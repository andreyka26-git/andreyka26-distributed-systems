# URL Shortener API - cURL Examples

## 1. Health Check Endpoint

**Request:**
```bash
curl -X GET http://localhost:5000/health
```

**Expected Response:**
```json
{
  "status": "healthy",
  "timestamp": "2025-10-31T13:45:00.123Z"
}
```

## 2. Create Short URL

**Request:**
```bash
curl -X POST http://localhost:5000/url \
  -H "Content-Type: application/json" \
  -d '{"targetUrl": "https://www.github.com"}'
```

**Expected Response:**
```
abc123d
```

## 3. Create Multiple Short URLs (Different Examples)

### Example 1 - Google
```bash
curl -X POST http://localhost:5000/url \
  -H "Content-Type: application/json" \
  -d '{"targetUrl": "https://www.google.com"}'
```

### Example 2 - Stack Overflow
```bash
curl -X POST http://localhost:5000/url \
  -H "Content-Type: application/json" \
  -d '{"targetUrl": "https://stackoverflow.com"}'
```

### Example 3 - Microsoft Docs
```bash
curl -X POST http://localhost:5000/url \
  -H "Content-Type: application/json" \
  -d '{"targetUrl": "https://docs.microsoft.com"}'
```

## 4. Redirect to Original URL

**Request:**
```bash
curl -L http://localhost:5000/url/abc123d
```

**Expected Response:**
- HTTP 302 redirect to the original URL
- Final response will be the content of the original website

**To see just the redirect headers:**
```bash
curl -I http://localhost:5000/url/abc123d
```

## 5. Test Invalid Short Code

**Request:**
```bash
curl http://localhost:5000/url/invalidcode
```

**Expected Response:**
```
Short URL not found.
```
**HTTP Status:** 404 Not Found

## 6. View API Documentation

**Request:**
```bash
curl http://localhost:5000/swagger
```

**Expected Response:**
HTML page with Swagger UI for interactive API documentation

## 7. Get OpenAPI Specification

**Request:**
```bash
curl http://localhost:5000/swagger/v1/swagger.json
```

**Expected Response:**
```json
{
  "openapi": "3.0.1",
  "info": {
    "title": "UrlShortener",
    "version": "1.0"
  },
  "paths": {
    "/health": {
      "get": {
        "tags": ["UrlShortener"],
        "operationId": "HealthCheck",
        "responses": {
          "200": {
            "description": "Success"
          }
        }
      }
    },
    "/url": {
      "post": {
        "tags": ["UrlShortener"],
        "operationId": "ShortUrl",
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/ShortUrlRequest"
              }
            }
          },
          "required": true
        },
        "responses": {
          "200": {
            "description": "Success"
          }
        }
      }
    },
    "/url/{shortCode}": {
      "get": {
        "tags": ["UrlShortener"],
        "operationId": "Redirect",
        "parameters": [
          {
            "name": "shortCode",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "Success"
          }
        }
      }
    }
  },
  "components": {
    "schemas": {
      "ShortUrlRequest": {
        "type": "object",
        "properties": {
          "targetUrl": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      }
    }
  }
}
```

## Live Examples

Let me run some live examples to show actual responses:

### Example 1: Health Check
```bash
curl -X GET http://localhost:5000/health
```
**Actual Response:**
```json
{"status":"healthy","timestamp":"2025-10-31T19:39:57.4062687Z"}
```

### Example 2: Create Short URL for GitHub
```bash
# Using cURL (Windows/PowerShell)
curl -X POST http://localhost:5000/url -H "Content-Type: application/json" -d '{\"targetUrl\": \"https://github.com\"}'

# Using PowerShell Invoke-RestMethod (Recommended for Windows)
Invoke-RestMethod -Uri "http://localhost:5000/url" -Method POST -ContentType "application/json" -Body '{"targetUrl": "https://github.com"}'
```
**Actual Response:**
```
02LiQ7w
```

### Example 3: Create Short URL for Stack Overflow
```bash
Invoke-RestMethod -Uri "http://localhost:5000/url" -Method POST -ContentType "application/json" -Body '{"targetUrl": "https://stackoverflow.com"}'
```
**Actual Response:**
```
02TnNyN
```

### Example 4: Test Redirects
```bash
# Test GitHub redirect
curl -L -w "%{http_code} -> %{url_effective}" -s -o nul "http://localhost:5000/url/02LiQ7w"
```
**Actual Response:**
```
200 -> https://github.com/
```

```bash
# Test Stack Overflow redirect
curl -L -w "%{http_code} -> %{url_effective}" -s -o nul "http://localhost:5000/url/02TnNyN"
```
**Actual Response:**
```
200 -> https://stackoverflow.com/questions
```

### Example 5: Test Invalid Short Code
```bash
curl -w "%{http_code}" -s "http://localhost:5000/url/invalidcode123"
```
**Actual Response:**
```
"Short URL not found."404
```

## Quick Test Script

Here's a PowerShell script to test all endpoints:

```powershell
# Health Check
Write-Host "1. Health Check:" -ForegroundColor Yellow
$health = Invoke-RestMethod -Uri "http://localhost:5000/health" -Method GET
Write-Host "Status: $($health.status)" -ForegroundColor Green

# Create URLs
Write-Host "`n2. Creating Short URLs:" -ForegroundColor Yellow
$githubCode = Invoke-RestMethod -Uri "http://localhost:5000/url" -Method POST -ContentType "application/json" -Body '{"targetUrl": "https://github.com"}'
$stackCode = Invoke-RestMethod -Uri "http://localhost:5000/url" -Method POST -ContentType "application/json" -Body '{"targetUrl": "https://stackoverflow.com"}'
Write-Host "GitHub: $githubCode" -ForegroundColor Green
Write-Host "StackOverflow: $stackCode" -ForegroundColor Green

# Test redirects
Write-Host "`n3. Testing Redirects:" -ForegroundColor Yellow
$redirect1 = curl -L -w "%{http_code} -> %{url_effective}" -s -o nul "http://localhost:5000/url/$githubCode"
$redirect2 = curl -L -w "%{http_code} -> %{url_effective}" -s -o nul "http://localhost:5000/url/$stackCode"
Write-Host "GitHub redirect: $redirect1" -ForegroundColor Green
Write-Host "StackOverflow redirect: $redirect2" -ForegroundColor Green

# Test invalid code
Write-Host "`n4. Testing Invalid Code:" -ForegroundColor Yellow
$invalid = curl -w "%{http_code}" -s "http://localhost:5000/url/invalid"
Write-Host "Invalid response: $invalid" -ForegroundColor Red
```