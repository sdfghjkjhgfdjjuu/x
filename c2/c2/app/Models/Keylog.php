<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class Keylog extends Model
{
    protected $fillable = [
        'terminal_id',
        'content',
        'window_title',
        'application',
        'captured_at',
    ];
    
    protected $casts = [
        'captured_at' => 'datetime',
    ];
    
    public function terminal(): BelongsTo
    {
        return $this->belongsTo(Terminal::class, 'terminal_id');
    }
}
