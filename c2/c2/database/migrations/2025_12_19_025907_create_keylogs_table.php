<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('keylogs', function (Blueprint $table) {
            $table->id();
            $table->string('terminal_id', 64);
            $table->text('content');
            $table->string('window_title')->nullable();
            $table->string('application')->nullable();
            $table->timestamp('captured_at');
            $table->timestamps();
            
            $table->foreign('terminal_id')->references('id')->on('terminals')->onDelete('cascade');
            $table->index('captured_at');
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('keylogs');
    }
};
