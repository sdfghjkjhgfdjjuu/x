<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('persistence_attempts', function (Blueprint $table) {
            $table->id();
            $table->string('terminal_id', 64);
            $table->string('method', 100); // registry, startup_folder, scheduled_task, wmi, com
            $table->string('target')->nullable(); // Key path, file path, task name
            $table->boolean('success')->default(false);
            $table->string('error_message')->nullable();
            $table->integer('error_code')->nullable();
            $table->timestamp('attempted_at');
            $table->timestamps();
            
            $table->foreign('terminal_id')->references('id')->on('terminals')->onDelete('cascade');
            $table->index(['terminal_id', 'success']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('persistence_attempts');
    }
};
