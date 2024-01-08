echo 'start'

docker run --name event-forging-tests-event-store-db -it -p 2113:2113 eventstore/eventstore:23.10.0-jammy --insecure --run-projections=All --enable-atom-pub-over-http
