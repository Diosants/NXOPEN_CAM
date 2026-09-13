using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace nxteste2
{
    // Guarda a chave da API Anthropic FORA do código-fonte, pra nunca ficar
    // hardcoded/commitada no repositório.
    //
    // Ordem de busca, na primeira vez que alguma tela pede a chave:
    //   1) Variável de ambiente ANTHROPIC_API_KEY — recomendado. No Windows:
    //      Painel de Controle → Sistema → Configurações avançadas → Variáveis
    //      de Ambiente → Nova (variável do usuário).
    //   2) Arquivo local criptografado em
    //      %AppData%\PathNCAutomation\anthropic.key.
    //   3) Se nenhum dos dois existir, abre uma caixa de diálogo (campo
    //      mascarado) pedindo a chave, e salva criptografada pra próxima vez.
    //
    // A criptografia do arquivo usa AES (System.Security.Cryptography.Aes),
    // com a chave derivada do nome da máquina + usuário do Windows via
    // SHA-256 — não a DPAPI (ProtectedData), porque essa classe vive numa
    // assembly separada (System.Security no .NET Framework, ou o pacote
    // NuGet System.Security.Cryptography.ProtectedData no .NET moderno) que
    // não estava resolvendo no seu projeto. Aes e SHA256 vêm no BCL padrão
    // de qualquer projeto .NET, sem referência extra nenhuma.
    //
    // Isso não é tão forte quanto a DPAPI (que usa o cofre de credenciais do
    // Windows), mas cumpre o objetivo prático: o arquivo em disco não fica
    // em texto puro, e só descriptografa nessa mesma máquina/conta.
    public static class ApiKeyStore
    {
        private static readonly string KeyFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PathNCAutomation");
        private static readonly string KeyFilePath = Path.Combine(KeyFolder, "anthropic.key");

        private static string _cachedKey;

        public static string GetApiKey()
        {
            if (!string.IsNullOrWhiteSpace(_cachedKey))
                return _cachedKey;

            string fromEnv = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                _cachedKey = fromEnv;
                return _cachedKey;
            }

            string fromFile = LoadFromDisk();
            if (!string.IsNullOrWhiteSpace(fromFile))
            {
                _cachedKey = fromFile;
                return _cachedKey;
            }

            string typed = PromptForKey();
            if (!string.IsNullOrWhiteSpace(typed))
            {
                SaveToDisk(typed);
                _cachedKey = typed;
            }
            return _cachedKey;
        }

        // Chame isso (ex.: botão "Trocar Chave de API" na tela AI Assistant)
        // pra esquecer a chave atual e forçar o prompt na próxima mensagem.
        public static void ClearSavedKey()
        {
            _cachedKey = null;
            try
            {
                if (File.Exists(KeyFilePath))
                    File.Delete(KeyFilePath);
            }
            catch
            {
                // Não é crítico se o delete falhar (ex.: arquivo em uso) — a
                // chave em memória já foi esquecida, então o próximo
                // SendAsync já vai pedir de novo mesmo que o arquivo antigo
                // ainda exista (será sobrescrito ao salvar a nova).
            }
        }

        private static string PromptForKey()
        {
            using (ApiKeyPromptForm dlg = new ApiKeyPromptForm())
            {
                return dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK
                    ? dlg.EnteredKey
                    : null;
            }
        }

        private static string LoadFromDisk()
        {
            try
            {
                if (!File.Exists(KeyFilePath))
                    return null;
                byte[] encrypted = File.ReadAllBytes(KeyFilePath);
                byte[] plain = Decrypt(encrypted);
                return Encoding.UTF8.GetString(plain);
            }
            catch
            {
                // Arquivo corrompido, copiado de outra máquina/usuário
                // (a chave derivada não bate), etc. Trata como "sem chave
                // salva" e deixa pedir de novo.
                return null;
            }
        }

        private static void SaveToDisk(string key)
        {
            try
            {
                Directory.CreateDirectory(KeyFolder);
                byte[] plain = Encoding.UTF8.GetBytes(key);
                byte[] encrypted = Encrypt(plain);
                File.WriteAllBytes(KeyFilePath, encrypted);
            }
            catch
            {
                // Se não conseguir gravar em disco (permissão, disco cheio,
                // etc.), a sessão atual ainda funciona porque a chave já
                // está em _cachedKey — só não persiste pra próxima vez.
            }
        }

        // ── Criptografia local (AES-256), sem dependência de DPAPI ────────

        private static byte[] DeriveKey()
        {
            string material = Environment.MachineName + "|" + Environment.UserName + "|PathNCAutomation.AnthropicKey";
            using (SHA256 sha = SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes(material));
            }
        }

        private static byte[] Encrypt(byte[] plain)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = DeriveKey();
                aes.GenerateIV();
                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                using (MemoryStream ms = new MemoryStream())
                {
                    // Guarda o IV (16 bytes) na frente do arquivo — não é
                    // segredo, só precisa ser único por gravação, e é lido
                    // de volta no Decrypt.
                    ms.Write(aes.IV, 0, aes.IV.Length);
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(plain, 0, plain.Length);
                        cs.FlushFinalBlock();
                    }
                    return ms.ToArray();
                }
            }
        }

        private static byte[] Decrypt(byte[] data)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = DeriveKey();
                byte[] iv = new byte[16];
                Array.Copy(data, 0, iv, 0, iv.Length);
                aes.IV = iv;
                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                using (MemoryStream cipherStream = new MemoryStream(data, iv.Length, data.Length - iv.Length))
                using (CryptoStream cs = new CryptoStream(cipherStream, decryptor, CryptoStreamMode.Read))
                using (MemoryStream plainStream = new MemoryStream())
                {
                    cs.CopyTo(plainStream);
                    return plainStream.ToArray();
                }
            }
        }
    }
}
