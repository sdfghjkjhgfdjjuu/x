<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class TelemetryLog extends Model
{
    protected $fillable = [
        'terminal_id',
        'level',
        'module',
        'message',
        'exception_type',
        'stack_trace',
        'context',
        'logged_at',
    ];
    
    protected $casts = [
        'context' => 'array',
        'logged_at' => 'datetime',
    ];
    
    public function terminal(): BelongsTo
    {
        return $this->belongsTo(Terminal::class, 'terminal_id');
    }
    
    /**
     * Scope for filtering by level
     */
    public function scopeLevel($query, string $level)
    {
        return $query->where('level', $level);
    }
    
    /**
     * Scope for errors only
     */
    public function scopeErrors($query)
    {
        return $query->whereIn('level', ['Error', 'Critical']);
    }
}
