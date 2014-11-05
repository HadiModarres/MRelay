using System;

/*
 * To change this license header, choose License Headers in Project Properties.
 * To change this template file, choose Tools | Templates
 * and open the template in the editor.
 */
namespace phelix
{

	/// 
	/// <summary>
	///
	/// </summary>
	/// <summary>
	/// An implementation of the Phelix authenticating stream cipher as described in
	/// <br />
	/// <center>Phelix<br />
	/// Fast Encryption and Authentication<br />
	/// in a Single Cryptographic Primitive<br />
	/// </center> by Doug Whiting, Bruce Schneier, Stefan Lucks and
	/// Fr&#xE9;d&#xE9;rick Muller. See
	/// <a href="http://www.schneier.com/paper-phelix.pdf">http://www.schneier.com/paper-phelix.pdf</a>.
	/// <br />
	/// Note: This implementation does not give correct results if a message's
	/// Additional Authentication Data exceeds 4,294,967,295 bytes (4,294,967,295
	/// is one byte less than 4 gigabytes) or if the sum of the number of bytes
	/// of Additional Authentication Data plus the number of bytes of plaintext
	/// plus the number of bytes of MAC exceeds 8,589,934,529 bytes (63 bytes less
	/// than 8 gigabytes).
	/// <pre>
	/// Typical usage for encryption:
	///   byte[] key;
	///   byte[] nonce;
	///   byte[] plaintext;
	///   byte[] encrypted=new byte[plaintext.length+Phelix.MAX_MAC_BYTES];
	///   Phelix crypt;
	///   ...
	///   key=16 to 32 bytes of secret key.
	///   nonce=0 to 16 bytes containing a value not used before with this key.
	///   plaintext=data to be encrypted
	///   ...
	///   crypt=new Phelix(false, // Encrypting
	///    key, subscript of first byte of key, length of key in bytes,
	///    nonce, subscript of first byte of nonce, length of nonce in bytes);
	///   if (there's additional authentication data) {
	///      byte[] aad=...
	///      crypt.startAad();
	///      for (int jj=0; jj&lt;aad.length; ++jj) {
	///        crypt.next(aad[jj]);
	///      }
	///      crypt.endAad();
	///   }
	///   for (int jj=0; jj&lt;plaintext.length; ++jj) {
	///     encrypted[jj]=crypt.next(plaintext[jj]);
	///   }
	///   crypt.digest(encrypted, plaintext.length);
	///   // encrypted[] now contains the complete ciphertext and MAC.
	/// 
	/// 
	/// To encrypt another message with the same key and a different nonce:
	///   byte[] aDifferentNonce;
	///   byte[] anotherPlaintext;
	///   byte[] anotherCipher=new byte[anotherPlaintext.length+Phelix.MAX_MAC_BYTES];
	///   ...
	///   crypt.setNonce(false, aDifferentNonce);
	///   if (there's additional authentication data) {
	///      byte[] anotherAad=...
	///      crypt.startAad();
	///      for (int jj=0; jj&lt;anotherAad.length; ++jj) {
	///        crypt.next(anotherAad[jj]);
	///      }
	///      crypt.endAad();
	///   }
	///   for (int jj=0; jj&lt;anotherPlaintext.length; ++jj) {
	///     anotherCipher[jj]=crypt.next(anotherPlaintext[jj]);
	///   }
	///   crypt.digest(anotherCipher, plaintext.length);
	///   // anotherCipher[] now contains another complete ciphertext and MAC.
	/// 
	/// 
	/// 
	/// Typical usage for decryption:
	///   byte[] key;   // Must contain the same key as used for encryption.
	///   byte[] nonce; // Must contain the same nonce as used for encryption.
	///   byte[] encrypted;  // Ciphertext and MAC from encryption
	///   byte[] message=new byte[encrypted.length-Phelix.MAX_MAC_BYTES];
	///   Phelix crypt;
	///   ...
	///   crypt=new Phelix(true, // Decrypting
	///    key, subscript of first byte of key, length of key in bytes,
	///    nonce, subscript of first byte of nonce, length of nonce in bytes);
	///   if (there's additional authentication data) {
	///      byte[] aad=...
	///      crypt.startAad();
	///      for (int jj=0; jj&lt;aad.length; ++jj) {
	///        crypt.next(aad[jj]);
	///      }
	///      crypt.endAad();
	///   }
	///   for (int jj=0; jj&lt;message.length; ++jj) {
	///     message[jj]=crypt.next(encrypted[jj]);
	///   }
	///   byte[] calculatedMac=new byte[Phelix.MAX_MAC_BYTES];
	///   crypt.digest(calculatedMac, 0);
	///   for (int jj=0; jj&lt;Phelix.MAX_MAC_BYTES; ++jj) {
	///     // Compare received MAC with calculated MAC.
	///     if (encrypted[messge.length+jj]!=calculatedMac[jj]) {
	///       for (int kk=0; kk&lt;message.length; ++kk) {
	///         // Do not reveal plaintext if MAC verification fails.
	///         message[kk]=0;
	///       }
	///       throw new RuntimeException("Invalid MAC");
	///     }
	///   }
	///   // message[] now contains the original plaintext.
	///  
	/// </pre>
	/// Usage notes from the defining paper:
	/// <ul><li>
	/// The sender must ensure that each (Key,Nonce) pair is used at most once
	/// to encrypt a message. A sender using a single key to send more than one
	/// message must use a new and unique nonce for each message. Multiple
	/// senders that want to use the same key must employ a scheme that divides
	/// the nonce space into non-overlapping sets, in order to ensure that the
	/// same nonce is never used twice. If two different messages are ever
	/// encrypted with the same (Key,Nonce) pair, Phelix loses most of its
	/// security properties.</li>
	/// <li>
	/// The receiver may not release the plaintext, or the keystream, until it
	/// has verified the MAC successfully. In most situations, this requires
	/// the receiver to buffer the entire plaintext before it is released.</li>
	/// <li>
	/// These requirements seem restrictive, but they are in fact required by
	/// all stream ciphers and many block cipher modes (e.g., OCB and CCM).</li>
	/// <li>
	/// Although Phelix allows the use of short keys, we strongly recommend
	/// the use of keys of at least 128 bits, preferably 256 bits.</li>
	/// </ul>
	/// End of usage notes from the defining paper.
	/// <pre>
	///  Copyright (C) 2006 Michael Amling
	/// 
	///  This library is free software; you can redistribute it and/or
	///  modify it under the terms of the GNU Lesser General Public
	///  License as published by the Free Software Foundation; either
	///  version 2.1 of the License, or (at your option) any later version.
	/// 
	///  This library is distributed in the hope that it will be useful,
	///  but WITHOUT ANY WARRANTY; without even the implied warranty of
	///  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
	///  Lesser General Public License for more details.
	/// 
	///  You should have received a copy of the GNU Lesser General Public
	///  License along with this library; if not, write to the
	///  Free Software Foundation, Inc.
	///  51 Franklin Street, Fifth Floor
	///  Boston, MA  02110-1301  USA
	/// </pre>
	/// @author Mike Amling mamling@gmail.com
	/// </summary>
	public sealed class Phelix
	{

	/// <summary>
	/// Allow keys of less than 128 bits for testing only. </summary>
	private const bool TESTING = false;

	/// <summary>
	/// The key must be 16 to 32 bytes (except when TESTING) </summary>
	public const int MIN_KEY_BYTES = 16, MAX_KEY_BYTES = 32;

	/// <summary>
	/// The nonce must be no more than 16 bytes. </summary>
	public const int MIN_NONCE_BYTES = 0, MAX_NONCE_BYTES = 16;

	/// <summary>
	/// The Message Authenticaton Code must be no more than 16 bytes=128 bits. </summary>
	public const int MIN_MAC_BITS = 0, MAX_MAC_BITS = 128, MAX_MAC_BYTES = (MAX_MAC_BITS + 7) / 8;

	/// <summary>
	/// Used to recognize that AAD follows ciphertext </summary>
	private const int CHUG_BYTES = 32;

	/// <summary>
	/// Rotation constants for Phelix block function; The naming convention
	/// is from phelix.c in
	/// <a href="http://www.schneier.com/code/phelix.zip">http://www.schneier.com/code/phelix.zip</a>.
	/// </summary>
	private const int ROL_0A = 9, ROL_1A = 10, ROL_2A = 17, ROL_3B = 15, ROL_4B = 25, ROL_0B = 20, ROL_1B = 11, ROL_2B = 5, ROL_3A = 30, ROL_4A = 13;

	/// <summary>
	/// Constant for Additional Authentication Data </summary>
	private const int AAD_ARBITRARY = unchecked((int)0xAADaadAA);

	/// <summary>
	/// Constant for Message Authentication Code generation; This is a 1 bit,
	/// followed by the six low-order bits of the ASCII representations of
	/// each of the five characters "Helix", followed by a 1 bit.
	/// </summary>
	private const int MAC_ARBITRARY = unchecked((int)0x912D94F1);


	// Instance variables

	/// <summary>
	/// Working nonce </summary>
	[NonSerialized]
	private readonly int[] nn = new int[8];

	/// <summary>
	/// Working key </summary>
	[NonSerialized]
	private readonly int[] kk = new int[8];

	/// <summary>
	/// Length of key and MAC </summary>
	[NonSerialized]
	private readonly int lu4;

	/// <summary>
	/// Length of MAC </summary>
	[NonSerialized]
	private readonly int macBitLen;

	/// <summary>
	/// Values of <code>aa4</code> from previous rounds </summary>
	[NonSerialized]
	private int[] zold = new int[4];

	/// <summary>
	/// The working state of <code>Phelix</code> </summary>
	[NonSerialized]
	private int aa0, aa1, aa2, aa3, aa4;

	/// <summary>
	/// Round number </summary>
	[NonSerialized]
	private int ii;

	/// <summary>
	/// Total bytes of AAD, plaintext and MAC </summary>
	[NonSerialized]
	private int plainLen;

	/// <summary>
	/// Four bytes of plaintext to be mixed into <code>aa0</code> </summary>
	[NonSerialized]
	private int plainword;

	/// <summary>
	/// Four bytes of key stream </summary>
	[NonSerialized]
	private int streamword;

	/// <summary>
	/// Shift amount for combining bytes into ints </summary>
	[NonSerialized]
	private int shift = 24;

	/// <summary>
	/// Number of bytes of Additional Authentication Data </summary>
	[NonSerialized]
	private int aadLen;

	/// <summary>
	/// <code>true</code> if decrypting, <code>false</code> if encrypting </summary>
	[NonSerialized]
	private bool decrypting;

	/// <summary>
	/// Value of <code>decrypting</code> saved during AAD </summary>
	[NonSerialized]
	private bool aadec;

	/// <summary>
	/// <code>true</code> iff <code>startAad</code> has been called since the
	/// most recent call to <code>setNonce</code>, <code>endAad</code> or the
	/// constructor
	/// </summary>
	[NonSerialized]
	private bool inAad;

	/// <summary>
	/// <code>true</code> iff <code>digest</code> has not been called since
	/// the most recent call to <code>setNonce</code> or the constructor
	/// </summary>
	[NonSerialized]
	private bool ready;

	/// <summary>
	/// Invokes <code>clear</code> on the passed instance if it is not <code>null</code>. </summary>
	/// <param name="what"> an instance of <code>Phelix</code>, which may be <code>null</code> </param>
//JAVA TO C# CONVERTER WARNING: 'final' parameters are not available in .NET:
//ORIGINAL LINE: public static void clear(final Phelix what)
	public static void clear(Phelix what)
	{
			if (what != null)
			{
					what.clear();
			}
	}

	/// <summary>
	/// Normal constructor. </summary>
	/// <param name="decrypting"> <code>true</code> for decryption, <code>false</code> for
	/// encryption </param>
	/// <param name="keyb"> <code>byte</code> array containing secret key; The constructor
	/// sets <code>keyb[koff</code>..<code>koff+klen-1]</code> to zero after using
	/// its contents. </param>
	/// <param name="koff"> subscript of first byte of secret key in <code>keyb</code> </param>
	/// <param name="klen"> number of bytes of secret key, in range 16..32 </param>
	/// <param name="ivb"> byte array containing the new nonce; May be
	/// <code>null</code> if <code>ivlen</code> is zero. </param>
	/// <param name="ivoff"> subscript of the first byte of nonce in <code>ivb</code> </param>
	/// <param name="ivlen"> number of bytes of nonce, in range 0..16 </param>
//JAVA TO C# CONVERTER WARNING: 'final' parameters are not available in .NET:
//ORIGINAL LINE: public Phelix(final boolean decrypting, final byte[] keyb, final int koff, final int klen, final byte[] ivb, final int ivoff, final int ivlen)
	public Phelix(bool decrypting, byte[] keyb, int koff, int klen, byte[] ivb, int ivoff, int ivlen) : this(decrypting, keyb, koff, klen, ivb, ivoff, ivlen, MAX_MAC_BITS)
	{
	}

	/// <summary>
	/// Alternate constructor to override the MAC length. Use this only if you
	/// understand section "6.2 Truncated MAC Values" of the defining paper and
	/// need a shorter MAC length. </summary>
	/// <param name="decrypting"> <code>true</code> for decryption, <code>false</code> for
	/// encryption </param>
	/// <param name="keyb"> <code>byte</code> array containing secret key; The constructor
	/// sets <code>keyb[koff</code>..<code>koff+klen-1]</code> to zero after using
	/// its contents. </param>
	/// <param name="koff"> subscript of first byte of secret key in <code>keyb</code> </param>
	/// <param name="klen"> number of bytes of secret key, in range 16..32 </param>
	/// <param name="ivb"> byte array containing the new nonce; May be
	/// <code>null</code> if <code>ivlen</code> is zero. </param>
	/// <param name="ivoff"> subscript of the first byte of nonce in <code>ivb</code> </param>
	/// <param name="ivlen"> number of bytes of nonce, in range 0..16 </param>
	/// <param name="macBits"> number of bits of MAC to be generated by <code>digest</code>,
	/// in range 0..128 </param>
//JAVA TO C# CONVERTER WARNING: 'final' parameters are not available in .NET:
//ORIGINAL LINE: public Phelix(final boolean decrypting, final byte[] keyb, final int koff, final int klen, final byte[] ivb, final int ivoff, final int ivlen, final int macBits)
	public Phelix(bool decrypting, byte[] keyb, int koff, int klen, byte[] ivb, int ivoff, int ivlen, int macBits) : base()
	{

			bool ok = false;
			try
			{
					

					if (klen > MAX_KEY_BYTES || klen < (TESTING?0:MIN_KEY_BYTES))
					{
							throw new System.ArgumentException("Expected key " + MIN_KEY_BYTES + ".." + MAX_KEY_BYTES + " bytes, not " + klen);
					}
					if (macBits > MAX_MAC_BITS || macBits < MIN_MAC_BITS)
					{
							throw new System.ArgumentException("Expected MAC length " + MIN_MAC_BITS + ".." + MAX_MAC_BITS + " bits, not " + macBits);
					}
					this.macBitLen = macBits;

//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int[] kk=this.kk;
					int[] kk = this.kk;
					for (int jj = 0; jj < klen; ++jj)
					{
							kk[jj / 4] |= (keyb[jj + koff] & 0xFF) << ((jj & 3) * 8);
							keyb[jj + koff] = 0;
					}

					for (int jj = 8, oo = 0; --jj >= 0;)
					{
							hh(hh(klen + 64, kk[oo + 3], kk[oo + 2], kk[oo + 1], kk[oo], 0, 0), this.aa3, this.aa2, this.aa1, this.aa0, 0, 0);
							oo ^= 4;
							kk[oo] ^= this.aa0;
							kk[oo + 1] ^= this.aa1;
							kk[oo + 2] ^= this.aa2;
							kk[oo + 3] ^= this.aa3;
					}

					this.lu4 = klen * 4 + ((macBits & 0x7F) << 8);

					setNonce(decrypting, ivb, ivoff, ivlen);
					ok = true;
			}
			finally
			{
					if (!ok)
					{
                        
							clear();
					}
			}
	}

	/// <summary>
	/// "Additional Authentication Data" (AAD) is data that is to be
	/// authenticated but not encrypted. In <code>Phelix</code>, all AAD must
	/// be supplied before any data to be encrypted or decrypted. To use AAD,
	/// <ul>
	/// <li>Call <code>startAad</code>.</li>
	/// <li>Call <code>next</code> with each byte of AAD. Discard the returned values.</li>
	/// <li>Call <code>endAad</code>.</li>
	/// </ul>
	/// before calling <code>next</code> with the first byte of plaintext (if
	/// encrypting) or ciphertext (if decrypting). </summary>
	/// <exception cref="java.lang.IllegalStateException"> if data has already been
	/// encrypted or decrypted, i.e., if <code>next</code> has been called since
	/// the most recent call to <code>setNonce</code>, <code>endAad</code> or the
	/// constructor </exception>
	public void startAad()
	{
		lock (this)
		{
				if (this.plainLen != CHUG_BYTES)
				{
						throw new System.InvalidOperationException("AAD after text");
				}
				if (this.inAad)
				{
						throw new System.InvalidOperationException("startAad after AAD");
				}
				if ((this.aadLen & 3) != 0)
				{
						throw new System.InvalidOperationException("Cannot resume AAD");
				}
				this.inAad = true;
				this.aa1 ^= AAD_ARBITRARY;
				this.aadec = this.decrypting; // Save decryption status for endAad.
				this.decrypting = false;
		}
	}

	/// <summary>
	/// "Additional Authentication Data" (AAD) is data that is to be
	/// authenticated but not encrypted. In <code>Phelix</code>, all AAD must
	/// be supplied before any data to be encrypted or decrypted. To use AAD,
	/// <ul>
	/// <li>Call <code>startAad</code>.</li>
	/// <li>Call <code>next</code> with each byte of AAD. Discard the returned values.</li>
	/// <li>Call <code>endAad</code>.</li>
	/// </ul>
	/// before calling <code>next</code> with the first byte of plaintext (if
	/// encrypting) or ciphertext (if decrypting). </summary>
	/// <exception cref="java.lang.IllegalStateException"> if not processing AAD, i.e.,
	/// if <code>startAad</code> has not been called since
	/// the most recent call to <code>setNonce</code>, <code>endAad</code> or
	/// the constructor </exception>
	public void endAad()
	{
		lock (this)
		{
				if (!this.inAad)
				{
						throw new System.InvalidOperationException("endAad before startAad");
				}
				this.aadLen = this.plainLen - CHUG_BYTES;
				while ((this.plainLen & 3) != 0)
				{
						this.next((byte)0);
				}
				this.inAad = false;
				this.aa1 ^= AAD_ARBITRARY;
				this.decrypting = this.aadec; // Restore decryption status saved by endAad.
		}
	}

	/// <summary>
	/// Encrypt or decrypt a byte, or note a byte of AAD.
	/// <ul>
	/// <li>If encrypting, the return value is a byte of ciphertext.</li>
	/// <li>If decrypting, the return value is a byte of plaintext.</li>
	/// <li>If processing AAD, the return value should be ignored.</li>
	/// </ul> </summary>
	/// <param name="textb"> a byte of plaintext (if encrypting) or
	/// ciphertext (if decrypting) or a byte of additional
	/// authentication data </param>
	/// <returns> a ciphertext byte if encrypting, a plaintext
	/// byte if decrypting, or unspecified (if AAD) </returns>
	/// <exception cref="java.lang.IllegalStateException"> if <code>digest</code> has
	/// been called since the most recent call to <code>setNonce</code> or the
	/// constructor </exception>
//JAVA TO C# CONVERTER WARNING: 'final' parameters are not available in .NET:
//ORIGINAL LINE: public synchronized byte next(final byte textb)
	public byte next(byte textb)
	{
		lock (this)
		{
				if (!this.ready)
				{
						throw new System.InvalidOperationException("next after digest");
				}
				++this.plainLen;
				if (this.shift == 24)
				{
						this.streamword = hh(this.aa4, this.aa3, this.aa2, this.aa1, this.aa0, 0, this.kk[this.ii & 7]) + this.zold[this.ii & 3];
						this.shift = 0;
						this.plainword = (this.decrypting?this.streamword ^ textb:textb) & 0xFF;
						return (byte)(((int)((uint)this.streamword >> this.shift)) ^ textb);
				}
				this.shift += 8;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final byte after=(byte)((this.streamword>>>this.shift)^textb);
				byte after = (byte)(((int)((uint)this.streamword >> this.shift)) ^ textb);
				this.plainword |= ((this.decrypting?after:textb) & 0xFF) << this.shift;
				if (this.shift == 24)
				{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int ii7=this.ii & 7;
						int ii7 = this.ii & 7;
						this.zold[ii7 & 3] = hh(this.aa4, this.aa3, this.aa2, this.aa1, this.aa0, this.plainword, this.kk[ii7 ^ 4] + this.nn[ii7] + this.ii + ((ii7 & 3) == 1?this.lu4:0));
						//((ii7 & 3)==3?this.ii>>31:0) Note: Wrong if ii>=2**31, i.e. 8 GBytes
						++ii;
				}
				return after;
		}
	}

	/// <summary>
	/// Calculate and retrieve the Message Authentication Code (MAC). Note: If
	/// decrypting, the caller must call <code>digest</code> and compare the
	/// returned MAC with the MAC that was calculated during encryption. </summary>
	/// <param name="mac"> byte array to get the Message Authentication Code </param>
	/// <param name="offset"> subscript of the first element of <code>mac</code> to be
	/// overwritten </param>
	/// <returns> as a convenience, the number of bytes overwritten in <code>mac</code> </returns>
//JAVA TO C# CONVERTER WARNING: 'final' parameters are not available in .NET:
//ORIGINAL LINE: public synchronized int digest(final byte[] mac, int offset)
	public int digest(byte[] mac, int offset)
	{
		lock (this)
		{
				if (inAad)
				{
						throw new System.InvalidOperationException("digest before endAad");
				}
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final byte plen=(byte)(this.plainLen & 3);
				byte plen = (byte)(this.plainLen & 3);
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final boolean decrypted=this.decrypting;
				bool decrypted = this.decrypting;
				try
				{
						this.decrypting = false;
						// Finish a partial block.
						switch (plen)
						{
						case 1:
								next((byte)0); // Fall through.
							goto case 2;
						case 2:
								next((byte)0); // Fall through.
							goto case 3;
						case 3:
								next((byte)0); // Fall through.
								break;
						}
						this.aa0 ^= MAC_ARBITRARY;
						//this.aa2^=this.aadLen>>32; Note: Wrong if aadLen>=2**32, i.e. 4 GBytes
						this.aa4 ^= this.aadLen;
						for (int jj = 8; --jj >= 0;)
						{
								next(plen);
								next((byte)0);
								next((byte)0);
								next((byte)0);
						}
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int macByteLen=(this.macBitLen+7)/8;
						int macByteLen = (this.macBitLen + 7) / 8;
						for (int jj = 0; jj < macByteLen; ++jj)
						{
								mac[offset++] = next((jj & 3) != 0?(byte)0:plen);
						}
						if (false && (this.macBitLen & 7) != 0) // KAT does not mask last byte.
						{
								mac[offset - 1] &= (byte)(0xFF >> (8 - (this.macBitLen & 7)));
						}
						return macByteLen;
				}
				finally
				{
						this.ready = false;
						this.decrypting = decrypted;
				}
		}
	}

	/// <summary>
	/// Calculate and retrieve the Message Authentication Code (MAC). Note: If
	/// decrypting, the caller must call <code>digest</code> and compare the
	/// returned MAC with the MAC that was calculated during encryption. </summary>
	/// <returns> the Message Authentication Code, in a new byte array </returns>
	public byte[] digest()
	{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final byte[] resultMac = new byte[(this.macBitLen+7)/8];
			byte[] resultMac = new byte[(this.macBitLen + 7) / 8];
			digest(resultMac, 0);
			return resultMac;
	}

	/// <summary>
	/// Overlay all key-related and other data with zeros. Note: Other copies
	/// of the key and other data may still be in memory.
	/// </summary>
	public void clear()
	{
		lock (this)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int[] cold=this.zold;
				int[] cold = this.zold;
				this.zold = null;
				this.shift = 24;
				this.plainword = 0;
				this.streamword = 0;
				this.aa4 = this.aa3 = this.aa2 = this.aa1 = this.aa0 = 0;
				this.plainLen = this.aadLen = 0;
				this.ii = 0;
				this.decrypting = this.aadec = this.inAad = this.ready = false;
				for (int jj = 8; --jj >= 0;)
				{
						this.kk[jj] = 0;
						this.nn[jj] = 0;
						if (jj < 4 && cold != null)
						{
								cold[jj] = 0;
						}
				}
		}
	}

//JAVA TO C# CONVERTER WARNING: 'final' parameters are not available in .NET:
//ORIGINAL LINE: private int hh(int w4, int w3, int w2, int w1, int w0, final int k0, final int k1)
	private int hh(int w4, int w3, int w2, int w1, int w0, int k0, int k1)
	{
			w0 += w3 ^ k0;
			w3 = (w3 << ROL_3B) | ((int)((uint)w3>>(32 - ROL_3B))); // Naming convention from phelix.c
			w1 += w4;
			w4 = (w4 << ROL_4B) | ((int)((uint)w4>>(32 - ROL_4B)));
			w2 ^= w0;
			w0 = (w0 << ROL_0A) | ((int)((uint)w0>>(32 - ROL_0A)));
			w3 ^= w1;
			w1 = (w1 << ROL_1A) | ((int)((uint)w1>>(32 - ROL_1A)));
			w4 += w2;
			w2 = (w2 << ROL_2A) | ((int)((uint)w2>>(32 - ROL_2A)));

			w0 ^= w3 + k1;
			w3 = (w3 << ROL_3A) | ((int)((uint)w3>>(32 - ROL_3A)));
			w1 ^= w4;
			w4 = (w4 << ROL_4A) | ((int)((uint)w4>>(32 - ROL_4A)));
			w2 += w0;
			w0 = (w0 << ROL_0B) | ((int)((uint)w0>>(32 - ROL_0B)));
			w3 += w1;
			w1 = (w1 << ROL_1B) | ((int)((uint)w1>>(32 - ROL_1B)));
			w4 ^= w2;
			w2 = (w2 << ROL_2B) | ((int)((uint)w2>>(32 - ROL_2B)));
			this.aa0 = w0;
			this.aa1 = w1;
			this.aa2 = w2;
			this.aa3 = w3;
			return this.aa4 = w4;
	}

	/// <summary>
	/// Abandon anything in progress and start over with the same key and
	/// the passed nonce. </summary>
	/// <param name="decrypto"> <code>true</code> to decrypt, <code>false</code> to encrypt </param>
	/// <param name="ivb"> <code>byte</code> array containing the new nonce; May be
	/// <code>null</code> if <code>ivlen</code> is zero. </param>
	/// <param name="ivoff"> subscript of the first byte of nonce in <code>ivb</code> </param>
	/// <param name="ivlen"> number of bytes of nonce </param>
//JAVA TO C# CONVERTER WARNING: 'final' parameters are not available in .NET:
//ORIGINAL LINE: public synchronized void setNonce(final boolean decrypto, final byte[] ivb, final int ivoff, final int ivlen)
	public void setNonce(bool decrypto, byte[] ivb, int ivoff, int ivlen)
	{
		lock (this)
		{
				bool ok = false;
				try
				{
						if (ivlen > MAX_NONCE_BYTES || ivlen < MIN_NONCE_BYTES)
						{
								throw new System.ArgumentException("Expected nonce " + MIN_NONCE_BYTES + ".." + MAX_NONCE_BYTES + " bytes, not " + ivlen);
						}
						for (int jj = 4; --jj >= 0;)
						{
								this.nn[jj] = 0;
						}
						for (int jj = 0; jj < ivlen; ++jj)
						{
								// Little-endian
								this.nn[jj / 4] |= (ivb[jj + ivoff] & 0xFF) << ((jj & 3) * 8);
								// Implicitly supplies ivb[ivlen..nn.length*4-1]=0.
						}
						for (int jj = 4; --jj >= 0;)
						{
								this.nn[jj + 4] = jj - this.nn[jj];
						}
        
						this.zold[0] = this.zold[1] = this.zold[2] = this.zold[3] = 0;
						this.aa4 = this.kk[7];
						this.aa3 = this.kk[6] ^ this.nn[3];
						this.aa2 = this.kk[5] ^ this.nn[2];
						this.aa1 = this.kk[4] ^ this.nn[1];
						this.aa0 = this.kk[3] ^ this.nn[0];
						this.ii = this.plainLen = this.aadLen = this.plainword = this.streamword = 0;
						this.shift = 24;
						if (this.inAad)
						{
								this.decrypting = this.aadec;
						}
						this.inAad = false;
        
						this.decrypting = false;
						this.ready = true;
						for (int jj = CHUG_BYTES; --jj >= 0;)
						{
								next((byte)0);
						}
						this.decrypting = decrypto;
						ok = true;
				}
				finally
				{
						if (!ok)
						{
								clear();
						}
				}
		}
	}

	/// <summary>
	/// Abandon anything in progress and start over with the same key and
	/// the passed nonce. </summary>
	/// <param name="decrypto"> <code>true</code> to decrypt, <code>false</code> to encrypt </param>
	/// <param name="ivb"> <code>byte</code> array containing the new nonce in
	/// <code>ivb[0</code>..<code>MAX_NONCE_BYTES-1]</code> </param>
//JAVA TO C# CONVERTER WARNING: 'final' parameters are not available in .NET:
//ORIGINAL LINE: public synchronized void setNonce(final boolean decrypto, final byte[] ivb)
	public void setNonce(bool decrypto, byte[] ivb)
	{
		lock (this)
		{
				setNonce(decrypto, ivb, 0, Phelix.MAX_NONCE_BYTES);
		}
	}
	}

}