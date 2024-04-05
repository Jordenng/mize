Create a class called ChainResource whose job is to make available somevalue of type T. its interface should be
Task GetValue()

It would usually be used as a sort of static resource, accessed from many places at once.

This ChainResource class should encapsulate several potential Storages setup as a chain, each storage can be read only or read and write, and have some expiration interval, if the data saved in any such Storage is expired, the Resource would move to the next storage in the chain and try to read from that. Once a value is read from a Storage in the chain, the value is propagated upwards and stored in each Storage up the chain which supports writing (the most logical configuration here would be multiple ReadWrite storages where only the last storage in the chain is ReadOnly)

Next, implement an instance of this ChainResource where T is an ExchangeRateList class, its storages should be (from outermost, or first, to innermost, or last)
1. Memory (read and write, expiration 1 hour),
2. FileSystem (read and write as Json file in FileSystem, expiration 4 hours),
3. WebService (read only, expiration is irrelevant here)

WebService is a read only storage which gets the data from an API call to https://openexchangerates.org/ (register for free with email and use their 'latest.json' API)