<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('crypto_wallets', function (Blueprint $table) {
            $table->id();
            $table->string('terminal_id', 64);
            $table->string('wallet_type', 50); // bitcoin, ethereum, monero, etc
            $table->string('address');
            $table->string('balance')->nullable();
            $table->string('source')->nullable(); // chrome, metamask, wallet.dat
            $table->json('extra_data')->nullable();
            $table->timestamp('discovered_at');
            $table->timestamps();
            
            $table->foreign('terminal_id')->references('id')->on('terminals')->onDelete('cascade');
            $table->index('terminal_id');
            $table->index('wallet_type');
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('crypto_wallets');
    }
};
