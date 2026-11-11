using System.Security.Cryptography;
using System.Text;
using Bestia.Aplicacao.Contratos;

namespace Bestia.Infraestrutura.Seguranca;

/// <summary>
/// Formato do valor cifrado: versão (1) • id da chave (1) • nonce (12) • tag (16) • texto cifrado.
/// A chave de cifra e a de token saem da chave mestra por HKDF; o índice cego usa uma chave própria,
/// para a rotação da chave de cifra não invalidar as buscas.
/// </summary>
public sealed class CriptografiaAesGcm : ICriptografia
{
    private const byte Versao = 1;
    private const int TamanhoDaChave = 32;
    private const int TamanhoDoNonce = 12;
    private const int TamanhoDaTag = 16;
    private const int TamanhoDoCabecalho = 2 + TamanhoDoNonce + TamanhoDaTag;

    private readonly Dictionary<byte, byte[]> _chavesDeCifra;
    private readonly byte _idDaChaveAtual;
    private readonly byte[] _chaveDeIndice;
    private readonly byte[] _chaveDeToken;

    public CriptografiaAesGcm(IReadOnlyDictionary<byte, byte[]> chavesMestras, byte idDaChaveAtual, byte[] chaveDeIndice)
    {
        _chavesDeCifra = chavesMestras.ToDictionary(par => par.Key, par => Derivar(par.Value, "cifra"));
        _idDaChaveAtual = idDaChaveAtual;
        _chaveDeIndice = chaveDeIndice;
        _chaveDeToken = Derivar(chavesMestras[idDaChaveAtual], "token");
    }

    public byte[] Cifrar(string texto)
    {
        var claro = Encoding.UTF8.GetBytes(texto);
        var valor = new byte[TamanhoDoCabecalho + claro.Length];
        valor[0] = Versao;
        valor[1] = _idDaChaveAtual;
        var nonce = valor.AsSpan(2, TamanhoDoNonce);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_chavesDeCifra[_idDaChaveAtual], TamanhoDaTag);
        aes.Encrypt(nonce, claro, valor.AsSpan(TamanhoDoCabecalho), valor.AsSpan(2 + TamanhoDoNonce, TamanhoDaTag));
        return valor;
    }

    public string Decifrar(byte[] cifrado)
    {
        if (cifrado.Length < TamanhoDoCabecalho || cifrado[0] != Versao || !_chavesDeCifra.TryGetValue(cifrado[1], out var chave))
            throw new CryptographicException("Valor cifrado inválido ou com chave desconhecida.");

        var claro = new byte[cifrado.Length - TamanhoDoCabecalho];
        using var aes = new AesGcm(chave, TamanhoDaTag);
        aes.Decrypt(
            cifrado.AsSpan(2, TamanhoDoNonce),
            cifrado.AsSpan(TamanhoDoCabecalho),
            cifrado.AsSpan(2 + TamanhoDoNonce, TamanhoDaTag),
            claro);
        return Encoding.UTF8.GetString(claro);
    }

    public byte[] IndiceCego(string texto) =>
        HMACSHA256.HashData(_chaveDeIndice, Encoding.UTF8.GetBytes(texto.Trim().ToLowerInvariant()));

    public byte[] HashDeToken(string token) =>
        HMACSHA256.HashData(_chaveDeToken, Encoding.UTF8.GetBytes(token));

    private static byte[] Derivar(byte[] mestra, string finalidade) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, mestra, TamanhoDaChave, info: Encoding.UTF8.GetBytes(finalidade));
}
