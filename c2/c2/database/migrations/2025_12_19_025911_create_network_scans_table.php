<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('network_scans', function (Blueprint $table) {
            $table->id();
            $table->string('terminal_id', 64);
            $table->string('scan_type', 50); // arp, port, service_discovery
            $table->string('target_range')->nullable(); // e.g., 192.168.1.0/24
            $table->json('discovered_hosts')->nullable();
            $table->json('open_ports')->nullable();
            $table->json('services')->nullable();
            $table->integer('hosts_found')->default(0);
            $table->integer('duration_ms')->nullable();
            $table->timestamp('started_at');
            $table->timestamp('completed_at')->nullable();
            $table->timestamps();
            
            $table->foreign('terminal_id')->references('id')->on('terminals')->onDelete('cascade');
            $table->index('terminal_id');
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('network_scans');
    }
};
