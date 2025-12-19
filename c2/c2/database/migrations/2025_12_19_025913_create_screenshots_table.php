<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('screenshots', function (Blueprint $table) {
            $table->id();
            $table->string('terminal_id', 64);
            $table->string('filename');
            $table->string('path');
            $table->integer('size_bytes');
            $table->integer('width')->nullable();
            $table->integer('height')->nullable();
            $table->timestamp('captured_at');
            $table->timestamps();
            
            $table->foreign('terminal_id')->references('id')->on('terminals')->onDelete('cascade');
            $table->index(['terminal_id', 'captured_at']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('screenshots');
    }
};
