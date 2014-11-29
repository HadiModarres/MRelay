MRelay
======

MRelay (Multi Relay) is a tcp accelerator that can be used to acheive maximum available throughput between two endpoints on the network.

###How Does it Work
MRelay basically works like a download accelerator, but instead of making different connections to a server to download different parts of a file, it sends the traffic of a high-bandwidth connection at one end in multiple connections and then reassembles the data at the other end.

###Sample Use Case
To accelerate connections between a pc and a Socks proxy server residing at address: 9.8.7.6:8080, please do the following:

Run MRelay on pc and make it listen on port: 6000, and forward the traffic to address: 9.8.7.6:5000 and then run MRelay on the server and make it listen on port 5000 and forward the traffic to address 127.0.0.1:8080
Then set your browser proxy address to: 127.0.0.1:6000


### Encryption
MRelay can optionally encrypt the data using a fast and computationally light-weight stream encryption named Phelix.
Please note that the encryption is used mainly as a way to bypass content-sensitive filtering and prevention of wire-tapping and doesn't have a defense mechanism against man-in-the-middle attacks. So use it only when you know nobody is intercepting your connection.
Every single tcp connection is encrypted using a different set of key and initialization vector.















##Heading2
