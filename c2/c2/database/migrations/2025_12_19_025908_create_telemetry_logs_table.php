<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('telemetry_logs', function (Blueprint $table) {
            $table->id();
            $table->string('terminal_id', 64);
            $table->enum('level', ['Debug', 'Info', 'Warning', 'Error', 'Critical'])->default('Info');
            $table->string('module', 100);
            $table->text('message');
            $table->string('exception_type')->nullable();
            $table->text('stack_trace')->nullable();
            $table->json('context')->nullable();
            $table->timestamp('logged_at');
            $table->timestamps();
            
            $table->foreign('terminal_id')->references('id')->on('terminals')->onDelete('cascade');
            $table->index(['terminal_id', 'level']);
            $table->index('logged_at');
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('telemetry_logs');
    }
};
