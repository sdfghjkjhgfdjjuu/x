<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class NetworkScan extends Model
{
    protected $fillable = [
        'terminal_id',
        'scan_type',
        'target_range',
        'discovered_hosts',
        'open_ports',
        'services',
        'hosts_found',
        'duration_ms',
        'started_at',
        'completed_at',
    ];
    
    protected $casts = [
        'discovered_hosts' => 'array',
        'open_ports' => 'array',
        'services' => 'array',
        'started_at' => 'datetime',
        'completed_at' => 'datetime',
    ];
    
    public function terminal(): BelongsTo
    {
        return $this->belongsTo(Terminal::class, 'terminal_id');
    }
}
