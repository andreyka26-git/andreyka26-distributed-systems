## How the system works


Start client1, client2 and rootca
Make rootca to issue selfsigned rootca, and leaf per client
`docker compose up --build`

Call http://localhost:5001/call-client-2 to make sure the call client1 -> client2 works.

Stop client2 and rootca, which will make rootca to reissue root ca cert (selfsigned) and reissue client2 certificate again.
`docker compose restart client2 rootca`

Call http://localhost:5001/call-client-2 to make sure mTLS fails, because client2 has new rootca and new leaf referencing this root. Basically client1 and client2 have different authorities.
Client1 still keeps OLD root ca cert, as it is loaded once when app started

===

Now repeat the same except doing rotation for client 1 (check second leg of mTLS)

`docker compose restart client1 rootca`