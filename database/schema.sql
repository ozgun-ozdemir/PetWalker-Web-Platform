CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

BEGIN TRANSACTION;
CREATE TABLE "ContactMessages" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ContactMessages" PRIMARY KEY AUTOINCREMENT,
    "FullName" TEXT NOT NULL,
    "Subject" TEXT NOT NULL,
    "Email" TEXT NOT NULL,
    "Body" TEXT NOT NULL,
    "SentAtUtc" TEXT NOT NULL
);

CREATE TABLE "Customers" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Customers" PRIMARY KEY AUTOINCREMENT,
    "FirstName" TEXT NOT NULL,
    "LastName" TEXT NOT NULL,
    "Email" TEXT NOT NULL,
    "Phone" TEXT NOT NULL,
    "PasswordHash" TEXT NOT NULL,
    "DateOfBirth" TEXT NOT NULL,
    "Gender" INTEGER NOT NULL,
    "Address" TEXT NOT NULL,
    "PhotoPath" TEXT NULL
);

CREATE TABLE "Pets" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Pets" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "WeightKg" REAL NOT NULL,
    "AgeYears" INTEGER NOT NULL,
    "Type" INTEGER NOT NULL,
    "Sex" INTEGER NOT NULL,
    "Breed" TEXT NOT NULL,
    "Color" TEXT NOT NULL,
    "OwnerId" INTEGER NOT NULL,
    CONSTRAINT "FK_Pets_Customers_OwnerId" FOREIGN KEY ("OwnerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Walkers" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Walkers" PRIMARY KEY AUTOINCREMENT,
    "FirstName" TEXT NOT NULL,
    "LastName" TEXT NOT NULL,
    "Address" TEXT NOT NULL,
    "Phone" TEXT NOT NULL,
    "DateOfBirth" TEXT NOT NULL,
    "Gender" INTEGER NOT NULL,
    "ExperienceYears" INTEGER NOT NULL,
    "PricePer30Min" REAL NOT NULL,
    "Bio" TEXT NULL,
    "OfferedDurations" TEXT NOT NULL,
    "LeftAtUtc" TEXT NULL,
    "CustomerId" INTEGER NULL,
    CONSTRAINT "FK_Walkers_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE SET NULL
);

CREATE TABLE "Messages" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Messages" PRIMARY KEY AUTOINCREMENT,
    "CustomerId" INTEGER NOT NULL,
    "WalkerId" INTEGER NOT NULL,
    "SentByCustomer" INTEGER NOT NULL,
    "Text" TEXT NOT NULL,
    "SentAtUtc" TEXT NOT NULL,
    CONSTRAINT "FK_Messages_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Messages_Walkers_WalkerId" FOREIGN KEY ("WalkerId") REFERENCES "Walkers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Walks" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Walks" PRIMARY KEY AUTOINCREMENT,
    "CustomerId" INTEGER NOT NULL,
    "PetId" INTEGER NOT NULL,
    "WalkerId" INTEGER NOT NULL,
    "StartsAt" TEXT NOT NULL,
    "DurationMinutes" INTEGER NOT NULL,
    "Price" REAL NOT NULL,
    "Status" INTEGER NOT NULL,
    "Rating" INTEGER NULL,
    "Review" TEXT NULL,
    CONSTRAINT "FK_Walks_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Walks_Pets_PetId" FOREIGN KEY ("PetId") REFERENCES "Pets" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Walks_Walkers_WalkerId" FOREIGN KEY ("WalkerId") REFERENCES "Walkers" ("Id") ON DELETE RESTRICT
);

INSERT INTO "Walkers" ("Id", "Address", "Bio", "CustomerId", "DateOfBirth", "ExperienceYears", "FirstName", "Gender", "LastName", "LeftAtUtc", "OfferedDurations", "Phone", "PricePer30Min")
VALUES (1, 'Kadıköy, İstanbul', 'Büyük ırklarla deneyimli, hafta içi her gün müsait.', NULL, '1995-04-12', 5, 'Ayşe', 1, 'Yılmaz', NULL, '30,60,120', '05320000001', 150.0);
SELECT changes();

INSERT INTO "Walkers" ("Id", "Address", "Bio", "CustomerId", "DateOfBirth", "ExperienceYears", "FirstName", "Gender", "LastName", "LeftAtUtc", "OfferedDurations", "Phone", "PricePer30Min")
VALUES (2, 'Ümraniye, İstanbul', 'Veteriner teknikeri, ilaç takibi yapabilir.', NULL, '1990-09-03', 8, 'Mehmet', 2, 'Kaya', NULL, '30,60,120', '05320000002', 200.0);
SELECT changes();

INSERT INTO "Walkers" ("Id", "Address", "Bio", "CustomerId", "DateOfBirth", "ExperienceYears", "FirstName", "Gender", "LastName", "LeftAtUtc", "OfferedDurations", "Phone", "PricePer30Min")
VALUES (3, 'Beşiktaş, İstanbul', 'Küçük ırklar ve yavru köpekler.', NULL, '1999-01-25', 2, 'Zeynep', 1, 'Demir', NULL, '30,60,120', '05320000003', 120.0);
SELECT changes();

INSERT INTO "Walkers" ("Id", "Address", "Bio", "CustomerId", "DateOfBirth", "ExperienceYears", "FirstName", "Gender", "LastName", "LeftAtUtc", "OfferedDurations", "Phone", "PricePer30Min")
VALUES (4, 'Kadıköy, İstanbul', 'Moda sahilinde yürüyüşler, hafta sonu müsait.', NULL, '1981-06-18', 12, 'Can', 2, 'Öztürk', NULL, '30,60,120', '05320000004', 140.0);
SELECT changes();


CREATE UNIQUE INDEX "IX_Customers_Email" ON "Customers" ("Email");

CREATE INDEX "IX_Messages_CustomerId_WalkerId_SentAtUtc" ON "Messages" ("CustomerId", "WalkerId", "SentAtUtc");

CREATE INDEX "IX_Messages_WalkerId" ON "Messages" ("WalkerId");

CREATE INDEX "IX_Pets_OwnerId" ON "Pets" ("OwnerId");

CREATE UNIQUE INDEX "IX_Walkers_CustomerId" ON "Walkers" ("CustomerId");

CREATE INDEX "IX_Walks_CustomerId" ON "Walks" ("CustomerId");

CREATE INDEX "IX_Walks_PetId" ON "Walks" ("PetId");

CREATE INDEX "IX_Walks_WalkerId" ON "Walks" ("WalkerId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261020134245_InitialCreate', '10.0.13');

COMMIT;

