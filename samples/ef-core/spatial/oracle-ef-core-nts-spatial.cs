// This ODP.NET EF Core sample demonstrates using Oracle database spatial data types with EF Core.
// EF Core maps to database spatial types using the NetTopologySuite library.
// In this sample, Oracle EF Core creates a table with a location column that stores NTS geometry data.
// Seven different NTS geometry types are inserted: point, linestring, polygon, multipoint, multilinestring, multipolygon, and geometry collection.
// The app then retrieves the inserted data. Next, it queries for all locations within 2 kilometers of the point.
// Oracle EF Core performs a single location update, followed by a batch update.
// Lastly, it deletes all the rows in the table.
// This sample requires EF Core 10 or higher. 
// Add the Oracle.EntityFrameworkCore.NetTopologySuite NuGet package to your project.
// Enter your Oracle connection string into the DbContextOptionsBuilder to run this app.

using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace OracleSpatial
{ 
    internal class Program
    {
        public class Place
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public Geometry? Location { get; set; } // NTS Geometry
        }
        public class AppDbContext : DbContext
        {
            public DbSet<Place> Places { get; set; }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                // Add the NetTopologySuite library.
                // Enter connection string below.
                optionsBuilder.UseOracle("data source=<DATA SOURCE>;user id=<USER ID>;password=<PASSWORD>;",
                  o => o.UseNetTopologySuite()); // Set the tolerance level if necessary
            }
            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Place>(b =>
                {
                    b.HasKey(e => e.Id);
                });
            }
        }

        private static void Main()
        {
            using var db = new AppDbContext();
            Console.WriteLine("Drop and recreate database...");
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
            Console.WriteLine("Database ready.\n");

            var places = new List<Place>();

            Console.WriteLine(">>> Create sample geometries...");

            // 1. Point
            places.Add(new Place
            {
                Name = "Monument Point",
                Location = new Point(78.4867, 17.385) { SRID = 4326 }
            });

            // 2. LineString
            places.Add(new Place
            {
                Name = "Outer Ring Road Segment",
                Location = new LineString(new[]
                {
                    new Coordinate(78.4867, 17.385),
                    new Coordinate(78.5, 17.4)
                })
                { SRID = 4326 }
            });

            // 3. Polygon (closed ring)
            places.Add(new Place
            {
                Name = "Lake Boundary",
                Location = new Polygon(new LinearRing(new[]
                {
                    new Coordinate(78.48, 17.38),
                    new Coordinate(78.49, 17.39),
                    new Coordinate(78.48, 17.40),
                    new Coordinate(78.48, 17.38) // Close the ring
                }))
                { SRID = 4326 }
            });

            // 4. MultiPoint
            places.Add(new Place
            {
                Name = "Metro Station Entrances",
                Location = new MultiPoint(new[]
                {
                    new Point(78.48, 17.38),
                    new Point(78.49, 17.39)
                })
                { SRID = 4326 }
            });

            // 5. MultiLineString
            places.Add(new Place
            {
                Name = "Metro Corridor Network",
                Location = new MultiLineString(new[]
                {
                    new LineString(new[]
                    {
                        new Coordinate(78.1, 17.1),
                        new Coordinate(78.2, 17.2)
                    }),
                    new LineString(new[]
                    {
                        new Coordinate(78.3, 17.3),
                        new Coordinate(78.4, 17.4)
                    })
                })
                { SRID = 4326 }
            });

            // 6. MultiPolygon
            places.Add(new Place
            {
                Name = "Local Parks",
                Location = new MultiPolygon(new[]
                {
                    new Polygon(new LinearRing(new[]
                    {
                        new Coordinate(78.1, 17.1),
                        new Coordinate(78.2, 17.1),
                        new Coordinate(78.2, 17.2),
                        new Coordinate(78.1, 17.1)
                    })),
                    new Polygon(new LinearRing(new[]
                    {
                        new Coordinate(78.3, 17.3),
                        new Coordinate(78.4, 17.3),
                        new Coordinate(78.4, 17.4),
                        new Coordinate(78.3, 17.3)
                    }))
                })
                { SRID = 4326 }
            });

            // 7. GeometryCollection (Point + Line)
            places.Add(new Place
            {
                Name = "City Highlights Collection",
                Location = new GeometryCollection(new Geometry[]
                {
                    new Point(78.5, 17.5),
                    new LineString(new[]
                    {
                        new Coordinate(78.6, 17.6),
                        new Coordinate(78.7, 17.7)
                    })
                })
                { SRID = 4326 }
            });

            Console.WriteLine($"Created {places.Count} geometry samples.");
            Console.WriteLine();

            // Insert
            using var context = new AppDbContext();
            Console.WriteLine("Inserting sample geometries into Oracle database...");
            context.Places.AddRange(places);
            context.SaveChanges();
            Console.WriteLine("Insert completed.\n");

            // Query: select all rows
            Console.WriteLine(">>> All locations in database:");
            places = context.Places.ToList();
            foreach (var p in places)
            {
                Console.WriteLine($"{p.Id}: {p.Name} - {p.Location}");
            }
            Console.WriteLine();

            // Query: Select locations within 2 kilometers of Monument Point.
            Console.WriteLine(">>> Places within 2 kilometers of Monument Point (78.4867, 17.385):");
            var point = new Point(78.4867, 17.385) { SRID = 4326 };
            var nearby = context.Places
              .Where(predicate: p => p.Location.IsWithinDistance(point, 2000)) // 2 km
              .ToList();

            foreach (var item in nearby)
            {
                Console.WriteLine("==========> " + item.Name);
            }
            Console.WriteLine();

            Console.WriteLine("Press 'Enter' key to perform location updates.");
            Console.ReadLine();

            // Update one location
            Console.WriteLine("Updating geometry for 'Metro Corridor Network'...");
            using (var dbContext = new AppDbContext())
            {
                var multiline = dbContext.Places.FirstOrDefault(p => p.Name == "Metro Corridor Network");

                if (multiline != null)
                {
                    var factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
                    var line1 = factory.CreateLineString(new[]
                    {
                        new Coordinate(0, 0),
                        new Coordinate(10, 10)
                    });
                    var line2 = factory.CreateLineString(new[]
                    {
                        new Coordinate(20, 20),
                        new Coordinate(30, 30)
                    });

                    var correctedMultiLine = factory.CreateMultiLineString(new[] { line1, line2 });

                    // Reassign geometry
                    multiline.Location = correctedMultiLine;

                    // Update the entity        
                    dbContext.SaveChanges();

                    Console.WriteLine("Update completed for 'Metro Corridor Network' geometry.");

                    // Show update
                    using (var dbupdate = new AppDbContext())
                    { 
                        var loc = from place in dbupdate.Places
                        where place.Name.Contains("Metro Corridor Network")
                        select place;
                        foreach (var p in loc)
                        {
                            Console.WriteLine($"{p.Location}");
                        }
                    }
                }
            }
            Console.WriteLine();

            // Batch update locations
            Console.WriteLine("Batch updating geometries where ID is greater than 4...");
            var geom = new LineString(new[]
            {
                new Coordinate(0, 0),
                new Coordinate(10, 10)
            })
            { SRID = 4326 };
            context.Places
              .Where(p => p.Id > 4)
              .ExecuteUpdate(setters => setters
              .SetProperty(p => p.Location, geom)
              .SetProperty(p => p.Name, p => p.Name + " - Updated")
              );

            Console.WriteLine("Batch update completed.");

            // Show update
            var batchupdate = new AppDbContext();
            places = batchupdate.Places.ToList();
            foreach (var p in places)
            {
                Console.WriteLine($"{p.Id}: {p.Name} - {p.Location}");
            }
            Console.WriteLine();

            // Delete
            Console.WriteLine("Press 'Enter' key to delete all places from the database.");
            Console.ReadLine();
            var toDelete = context.Places.Where(p => p.Id <= 10).ToList();
            context.Places.RemoveRange(toDelete);
            context.SaveChanges();
            Console.WriteLine("All rows deleted.");
            Console.WriteLine();
        }
    }
}
/* Copyright (c) 2025 Oracle and/or its affiliates. All rights reserved. */

/******************************************************************************
 *
 * You may not use the identified files except in compliance with The MIT
 * License (the "License.")
 *
 * You may obtain a copy of the License at
 * https://github.com/oracle/Oracle.NET/blob/master/LICENSE.txt
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*****************************************************************************/
