<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class Screenshot extends Model
{
    protected $fillable = [
        'terminal_id',
        'filename',
        'path',
        'size_bytes',
        'width',
        'height',
        'captured_at',
    ];
    
    protected $casts = [
        'captured_at' => 'datetime',
    ];
    
    public function terminal(): BelongsTo
    {
        return $this->belongsTo(Terminal::class, 'terminal_id');
    }
    
    /**
     * Get the URL for the screenshot
     */
    public function getUrlAttribute(): string
    {
        return url("/download/{$this->terminal_id}/{$this->filename}");
    }
}
