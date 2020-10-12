# Hive

Networking system which uses and Protobuf for message declaration, making it easy to consume messages in other frameworks and languages than C#.
Networking implemented with standard .Net Sockets.
Uses ECS for server logic, making it fast and lightweight when running headless. 
Currently has a channel system set up for sending and consuming messages on Client/Server. Channels are sequentially packed, channel by channel, to decrease message overhead.

# This library is WIP!
