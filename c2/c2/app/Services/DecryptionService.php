<?php

namespace App\Services;

use App\Models\Terminal;
use Illuminate\Support\Facades\Log;

class DecryptionService
{
    /**
     * Decrypt content from a specific terminal
     * Expected format: Base64(IV + EncryptedData)
     */
    public function decrypt(string $terminalId, string $cipherText): ?string
    {
        $terminal = Terminal::find($terminalId);
        if (!$terminal || !$terminal->hostname || !$terminal->username) {
            Log::warning("Cannot decrypt data for {$terminalId}: Missing hostname or username");
            return null; 
        }

        $key = $this->deriveKey($terminal->hostname, $terminal->username);
        
        try {
            // CipherText is Base64 encoded
            $data = base64_decode($cipherText);
            
            // Extract IV (first 16 bytes for AES-256-CBC)
            $ivLength = 16;
            if (strlen($data) <= $ivLength) {
                return null;
            }
            
            $iv = substr($data, 0, $ivLength);
            $encryptedPayload = substr($data, $ivLength);
            
            $decrypted = openssl_decrypt(
                $encryptedPayload,
                'AES-256-CBC',
                $key,
                OPENSSL_RAW_DATA,
                $iv
            );
            
            if ($decrypted === false) {
                 Log::error("OpenSSL decryption failed for {$terminalId}");
                 return null;
            }
            
            return $decrypted;
        } catch (\Exception $e) {
            Log::error("Decryption exception: " . $e->getMessage());
            return null;
        }
    }
    
    /**
     * Derive AES key from hostname and username (Matches C# SHA256 logic)
     */
    private function deriveKey(string $hostname, string $username): string
    {
        // C# Logic: SHA256.ComputeHash(Encoding.UTF8.GetBytes(MachineName + UserName))
        // PHP hash with binary output true returns raw bytes.
        $raw = $hostname . $username;
        return hash('sha256', $raw, true);
    }
}
