<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class CommandQueue extends Model
{
    protected $table = 'commands_queue';
    
    protected $fillable = [
        'terminal_id',
        'type',
        'command',
        'status',
        'result',
        'sent_at',
        'executed_at',
    ];
    
    protected $casts = [
        'sent_at' => 'datetime',
        'executed_at' => 'datetime',
    ];
    
    public function terminal(): BelongsTo
    {
        return $this->belongsTo(Terminal::class, 'terminal_id');
    }
    
    /**
     * Scope for pending commands
     */
    public function scopePending($query)
    {
        return $query->where('status', 'pending');
    }
    
    /**
     * Mark command as sent
     */
    public function markAsSent(): void
    {
        $this->update([
            'status' => 'sent',
            'sent_at' => now(),
        ]);
    }
    
    /**
     * Mark command as executed
     */
    public function markAsExecuted(?string $result = null): void
    {
        $this->update([
            'status' => 'executed',
            'result' => $result,
            'executed_at' => now(),
        ]);
    }
}
