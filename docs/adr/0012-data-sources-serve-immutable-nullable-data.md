# BeatManager's published values are read-only

BeatManager exists to be the one simple surface any part of the application reads for all things
musical, and everything it serves is read-only: a consumer can never write back or mutate what
the surface handed it, because the source stays trustworthy only while no reader can become a
writer.
