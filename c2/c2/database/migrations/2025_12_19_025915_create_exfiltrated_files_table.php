<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('exfiltrated_files', function (Blueprint $table) {
            $table->id();
            $table->string('terminal_id', 64);
            $table->string('filename');
            $table->string('original_name')->nullable();
            $table->string('path');
            $table->integer('size_bytes');
            $table->string('mime_type')->nullable();
            $table->boolean('is_decrypted')->default(false);
            $table->string('decrypted_path')->nullable();
            $table->timestamp('uploaded_at');
            $table->timestamps();
            
            $table->foreign('terminal_id')->references('id')->on('terminals')->onDelete('cascade');
            $table->index(['terminal_id', 'uploaded_at']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('exfiltrated_files');
    }
};
