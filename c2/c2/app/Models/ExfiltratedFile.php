<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class ExfiltratedFile extends Model
{
    protected $fillable = [
        'terminal_id',
        'filename',
        'original_name',
        'path',
        'size_bytes',
        'mime_type',
        'is_decrypted',
        'decrypted_path',
        'uploaded_at',
    ];
    
    protected $casts = [
        'is_decrypted' => 'boolean',
        'uploaded_at' => 'datetime',
    ];
    
    public function terminal(): BelongsTo
    {
        return $this->belongsTo(Terminal::class, 'terminal_id');
    }
    
    public function getUrlAttribute(): string
    {
        return url("/download/{$this->terminal_id}/{$this->filename}");
    }
}
