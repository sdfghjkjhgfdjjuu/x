<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class PersistenceAttempt extends Model
{
    protected $fillable = [
        'terminal_id',
        'method',
        'target',
        'success',
        'error_message',
        'error_code',
        'attempted_at',
    ];
    
    protected $casts = [
        'success' => 'boolean',
        'attempted_at' => 'datetime',
    ];
    
    public function terminal(): BelongsTo
    {
        return $this->belongsTo(Terminal::class, 'terminal_id');
    }
    
    /**
     * Scope for successful attempts
     */
    public function scopeSuccessful($query)
    {
        return $query->where('success', true);
    }
    
    /**
     * Scope for failed attempts
     */
    public function scopeFailed($query)
    {
        return $query->where('success', false);
    }
}
