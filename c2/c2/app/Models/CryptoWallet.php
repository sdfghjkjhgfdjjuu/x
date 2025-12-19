<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class CryptoWallet extends Model
{
    protected $fillable = [
        'terminal_id',
        'wallet_type',
        'address',
        'balance',
        'source',
        'extra_data',
        'discovered_at',
    ];
    
    protected $casts = [
        'extra_data' => 'array',
        'discovered_at' => 'datetime',
    ];
    
    public function terminal(): BelongsTo
    {
        return $this->belongsTo(Terminal::class, 'terminal_id');
    }
}
