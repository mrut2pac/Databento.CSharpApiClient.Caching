# Batch folder samples

**These files are not real market data.** They only mimic the layout of the folders a Databento batch job produces, so the tests can
read every format the cache takes. Every price, size, count, sequence number, statistic and trade or statistic time in them is invented.
They are not exchange data, and must not be used as such.

What they keep is the format: the folder files (`metadata.json`, `condition.json`, `manifest.json`), the DBN and JSON encodings, zstd or
not, the record types and their layouts, the split by day, week, symbol or none, and the symbols and instrument ids that tie each record to
a symbol. The job ids (`...-SAMPLEJOBA` and so on) are invented, and the account id in the manifests' download URLs is replaced with
`USERID`, so the URLs point nowhere.
