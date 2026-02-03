const getRandomBytes = (): Uint8Array => {
  const bytes = new Uint8Array(16)
  const cryptoRef = globalThis.crypto

  if (cryptoRef?.getRandomValues) {
    cryptoRef.getRandomValues(bytes)
    return bytes
  }

  for (let i = 0; i < bytes.length; i += 1) {
    bytes[i] = Math.floor(Math.random() * 256)
  }

  return bytes
}

type UUID = `${string}-${string}-${string}-${string}-${string}`

const fallbackRandomUUID = (): UUID => {
  const bytes = getRandomBytes()

  // RFC 4122 variant/version bits
  if (bytes.length >= 9) {
    bytes[6] = ((bytes[6] ?? 0) & 0x0f) | 0x40
    bytes[8] = ((bytes[8] ?? 0) & 0x3f) | 0x80
  }

  const hex = Array.from(bytes, byte => byte.toString(16).padStart(2, '0'))
  return `${hex[0]}${hex[1]}${hex[2]}${hex[3]}-${hex[4]}${hex[5]}-${hex[6]}${hex[7]}-${hex[8]}${hex[9]}-${hex[10]}${hex[11]}${hex[12]}${hex[13]}${hex[14]}${hex[15]}` as UUID
}

const ensureCrypto = (): void => {
  if (typeof globalThis.crypto === 'undefined') {
    const fallbackCrypto = {
      getRandomValues<T extends ArrayBufferView>(array: T): T {
        const bytes = new Uint8Array(array.buffer, array.byteOffset, array.byteLength)
        for (let i = 0; i < bytes.length; i += 1) {
          bytes[i] = Math.floor(Math.random() * 256)
        }
        return array
      },
    }
    ;(globalThis as typeof globalThis & { crypto: Crypto }).crypto = fallbackCrypto as Crypto
  }

  if (!globalThis.crypto.randomUUID) {
    try {
      Object.defineProperty(globalThis.crypto, 'randomUUID', {
        value: fallbackRandomUUID,
        writable: false,
        configurable: true,
      })
    } catch {
      ;(globalThis.crypto as Crypto & { randomUUID?: () => UUID }).randomUUID = fallbackRandomUUID
    }
  }
}

ensureCrypto()

export {}
