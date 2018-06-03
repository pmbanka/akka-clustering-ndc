/// > .paket\paket.exe generate-load-scripts --group Main --framework net461 --type fsx
#load @".paket/load/net461/main.group.fsx"

open System
open Akka.Cluster.Tools.Singleton
open Akkling

let configWithPort (port:int) =
    let config = Configuration.parse ("""
        akka {
          # loglevel = DEBUG
          actor {
            provider = cluster
          }
          remote {
            dot-netty.tcp {
              public-hostname = "localhost"
              hostname = "localhost"
              port = """ + port.ToString() + """
            }
          }
          cluster {
            roles = ["Worker"]
            seed-nodes = [ "akka.tcp://cluster-system@localhost:5000" ]
          }
        }
        """)
    config.WithFallback(ClusterSingletonManager.DefaultConfig())

let system1 = System.create "cluster-system" (configWithPort 5000)
let system2 = System.create "cluster-system" (configWithPort 5001)
