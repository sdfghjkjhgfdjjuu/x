<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('commands_queue', function (Blueprint $table) {
            $table->id();
            $table->string('terminal_id', 64);
            $table->string('type', 50); // shell, remote_control, file_download, config_update
            $table->text('command');
            $table->enum('status', ['pending', 'sent', 'executed', 'failed'])->default('pending');
            $table->text('result')->nullable();
            $table->timestamp('sent_at')->nullable();
            $table->timestamp('executed_at')->nullable();
            $table->timestamps();
            
            $table->foreign('terminal_id')->references('id')->on('terminals')->onDelete('cascade');
            $table->index(['terminal_id', 'status']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('commands_queue');
    }
};
