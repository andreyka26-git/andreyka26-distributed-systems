-- Migration script for AutoIncrementUniqueIdClient and URL Mappings
-- This script creates the database and tables if they don't exist

-- Create database (run this as a superuser if the database doesn't exist)
-- CREATE DATABASE urlshortener;

-- Connect to the urlshortener database and run the following:

-- Create the unique_ids table if it doesn't exist
CREATE TABLE IF NOT EXISTS unique_ids (
    id BIGSERIAL PRIMARY KEY,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Create the url_mappings table if it doesn't exist
CREATE TABLE IF NOT EXISTS url_mappings (
    short_url VARCHAR(255) PRIMARY KEY,
    original_url TEXT NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Create indexes for performance
CREATE INDEX IF NOT EXISTS idx_unique_ids_created_at ON unique_ids(created_at);
CREATE INDEX IF NOT EXISTS idx_url_mappings_created_at ON url_mappings(created_at);
CREATE INDEX IF NOT EXISTS idx_url_mappings_original_url ON url_mappings(original_url);

-- Optional: Insert a starting value if you want to begin from a specific number
-- This is useful if you're migrating from another system and want to avoid ID conflicts
-- INSERT INTO unique_ids (created_at) VALUES (NOW()) ON CONFLICT DO NOTHING;
-- SELECT setval('unique_ids_id_seq', 1000, false); -- Start from 1001

-- Verify the tables were created
SELECT 
    table_name, 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_name IN ('unique_ids', 'url_mappings')
ORDER BY table_name, ordinal_position;