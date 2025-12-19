<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('privilege_escalation_attempts', function (Blueprint $table) {
            $table->id();
            $table->string('terminal_id', 64);
            $table->string('technique', 100); // fodhelper, token_theft, exploit_ms16_032, etc
            $table->string('initial_level', 50); // User, Admin, System
            $table->string('target_level', 50)->nullable();
            $table->string('achieved_level', 50)->nullable();
            $table->boolean('success')->default(false);
            $table->string('error_message')->nullable();
            $table->json('details')->nullable(); // Additional info like process stolen from
            $table->timestamp('attempted_at');
            $table->timestamps();
            
            $table->foreign('terminal_id')->references('id')->on('terminals')->onDelete('cascade');
            $table->index(['terminal_id', 'success']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('privilege_escalation_attempts');
    }
};
