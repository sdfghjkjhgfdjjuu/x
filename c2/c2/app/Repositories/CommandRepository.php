<?php

namespace App\Repositories;

use App\Models\CommandQueue;
use Illuminate\Support\Collection;

class CommandRepository
{
    /**
     * Queue a command for a terminal
     */
    public function queue(string $terminalId, string $command, string $type = 'shell'): CommandQueue
    {
        return CommandQueue::create([
            'terminal_id' => $terminalId,
            'command' => $command,
            'type' => $type,
            'status' => 'pending',
        ]);
    }
    
    /**
     * Get pending commands for a terminal
     */
    public function getPending(string $terminalId): Collection
    {
        return CommandQueue::where('terminal_id', $terminalId)
            ->where('status', 'pending')
            ->orderBy('created_at')
            ->get();
    }
    
    /**
     * Mark commands as sent
     */
    public function markAsSent(array $commandIds): int
    {
        return CommandQueue::whereIn('id', $commandIds)
            ->update([
                'status' => 'sent',
                'sent_at' => now(),
            ]);
    }
    
    /**
     * Mark command as executed with result
     */
    public function markAsExecuted(int $commandId, ?string $result = null): bool
    {
        $command = CommandQueue::find($commandId);
        if (!$command) return false;
        
        $command->markAsExecuted($result);
        return true;
    }
    
    /**
     * Get command history for a terminal
     */
    public function getHistory(string $terminalId, int $limit = 50): Collection
    {
        return CommandQueue::where('terminal_id', $terminalId)
            ->orderBy('created_at', 'desc')
            ->limit($limit)
            ->get();
    }
}
