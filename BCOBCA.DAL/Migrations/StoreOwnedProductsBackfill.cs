namespace BCOBCA.DAL.Migrations;

internal static class StoreOwnedProductsBackfill
{
    // Runs inside the EF migration transaction, before removing the old many-to-many links.
    public const string Sql = """
        LOCK TABLE "StoreProducts", "Products", "ProductVariants", "ProductImages" IN ACCESS EXCLUSIVE MODE;
        CREATE TEMP TABLE ownership_split ON COMMIT DROP AS
        SELECT ranked."ProductId", ranked."StoreId",
            CASE WHEN ranked.position = 1 THEN ranked."ProductId"
                 ELSE md5('store-listing-v1:' || ranked."ProductId"::text || ':' || ranked."StoreId"::text)::uuid END AS "ListingId"
        FROM (
            SELECT link."ProductId", link."StoreId", row_number() OVER (
                PARTITION BY link."ProductId"
                ORDER BY (s."OwnerAccountId" = b."OwnerAccountId") DESC,
                    (s."Status" = 1 AND s."DeletedAt" IS NULL) DESC, s."Slug", s."Id") AS position
            FROM "StoreProducts" link
            JOIN "Products" p ON p."Id" = link."ProductId"
            JOIN "Brands" b ON b."Id" = p."BrandId"
            JOIN "Stores" s ON s."Id" = link."StoreId"
        ) ranked;

        INSERT INTO "Products"
        SELECT (jsonb_populate_record(NULL::"Products", to_jsonb(p) || jsonb_build_object(
            'Id', split."ListingId", 'StoreId', split."StoreId",
            'SKU', left(p."SKU", 65) || '-S' || replace(split."ListingId"::text, '-', ''),
            'Slug', left(p."Slug", 245) || '-s' || replace(split."ListingId"::text, '-', ''),
            'AffiliateBudget', 0, 'CreatedAt', now(), 'UpdatedAt', now()))).*
        FROM ownership_split split JOIN "Products" p ON p."Id" = split."ProductId"
        WHERE split."ListingId" <> split."ProductId";

        INSERT INTO "ProductVariants"
        SELECT (jsonb_populate_record(NULL::"ProductVariants", to_jsonb(v) || jsonb_build_object(
            'Id', md5('store-variant-v1:' || v."Id"::text || ':' || split."StoreId"::text)::uuid,
            'ProductId', split."ListingId",
            'SKU', left(v."SKU", 65) || '-S' || md5('store-variant-v1:' || v."Id"::text || ':' || split."StoreId"::text),
            'StockQuantity', 0, 'ReservedQuantity', 0))).*
        FROM ownership_split split JOIN "ProductVariants" v ON v."ProductId" = split."ProductId"
        WHERE split."ListingId" <> split."ProductId";

        INSERT INTO "ProductImages"
        SELECT (jsonb_populate_record(NULL::"ProductImages", to_jsonb(i) || jsonb_build_object(
            'Id', md5('store-image-v1:' || i."Id"::text || ':' || split."StoreId"::text)::uuid,
            'ProductId', split."ListingId"))).*
        FROM ownership_split split JOIN "ProductImages" i ON i."ProductId" = split."ProductId"
        WHERE split."ListingId" <> split."ProductId";

        UPDATE "Products" p SET "StoreId" = split."StoreId"
        FROM ownership_split split WHERE split."ListingId" = split."ProductId" AND p."Id" = split."ProductId";

        -- Unassigned historical records stay available for old orders, never public or purchasable.
        UPDATE "Products" SET "IsDeleted" = TRUE WHERE "StoreId" IS NULL;
        """;
}
