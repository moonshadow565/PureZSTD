# Pure C#/.net implementation of ZSTD decompression

Writen mostly by following [reference implementation](https://github.com/facebook/zstd/tree/dev/doc/educational_decoder).
Operates directly on Span<byte> and in many cases outperforms official C implementation packaged by arch linux.
