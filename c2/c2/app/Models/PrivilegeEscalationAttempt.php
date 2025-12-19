<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class PrivilegeEscalationAttempt extends Model
{
    protected $fillable = [
        'terminal_id',
        'technique',
        'initial_level',
        'target_level',
        'achieved_level',
        'success',
        'error_message',
        'details',
        'attempted_at',
    ];
    
    protected $casts = [
        'success' => 'boolean',
        'details' => 'array',
        'attempted_at' => 'datetime',
    ];
    
    public function terminal(): BelongsTo
    {
        return $this->belongsTo(Terminal::class, 'terminal_id');
    }
}
