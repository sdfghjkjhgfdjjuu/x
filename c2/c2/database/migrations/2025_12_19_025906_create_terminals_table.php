<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('terminals', function (Blueprint $table) {
            $table->string('id', 64)->primary(); // Terminal UUID
            $table->string('ip', 45);
            $table->string('local_ip', 45)->nullable();
            $table->string('mac_address', 100)->nullable();
            $table->string('hostname')->nullable();
            $table->string('username')->nullable();
            $table->string('os')->nullable();
            $table->string('arch', 20)->nullable();
            $table->string('version', 50)->nullable();
            $table->string('status')->default('unknown');
            $table->json('system_info')->nullable();
            $table->boolean('is_elevated')->default(false);
            $table->text('gpu')->nullable();
            $table->text('antivirus')->nullable();
            $table->json('disks')->nullable();
            $table->json('network_interfaces')->nullable();
            $table->json('browser_history')->nullable();
            $table->json('installed_programs')->nullable();
            $table->timestamp('last_seen_at');
            $table->timestamp('first_seen_at')->nullable();
            $table->timestamps();
            
            $table->index('last_seen_at');
            $table->index('is_elevated');
            $table->index('status');
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('terminals');
    }
};
