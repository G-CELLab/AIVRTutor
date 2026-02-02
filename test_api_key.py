#!/usr/bin/env python3
"""Quick test script to verify OpenAI API key"""
import sys

# Paste your API key here temporarily (delete after testing)
API_KEY = "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"

if API_KEY == "sk-YOUR-KEY-HERE":
    print("❌ Please edit this file and add your API key on line 6")
    sys.exit(1)

try:
    import requests
except ImportError:
    print("Installing requests...")
    import subprocess
    subprocess.check_call([sys.executable, "-m", "pip", "install", "requests"])
    import requests

# Test basic API access
print("Testing API key...")
headers = {
    "Authorization": f"Bearer {API_KEY}",
    "Content-Type": "application/json"
}

# Test 1: Basic models endpoint
response = requests.get("https://api.openai.com/v1/models", headers=headers)
print(f"\n1. Basic API access: {response.status_code}")

if response.status_code == 200:
    print("   ✅ API key is valid")
    models = response.json().get("data", [])
    
    # Check for realtime models
    realtime_models = [m["id"] for m in models if "realtime" in m["id"].lower()]
    if realtime_models:
        print(f"   ✅ Realtime models available: {realtime_models}")
    else:
        print("   ⚠️  No realtime models found - your account may not have Realtime API access")
        print("   Available models:", [m["id"] for m in models[:5]], "...")
elif response.status_code == 401:
    print("   ❌ Invalid API key (401 Unauthorized)")
elif response.status_code == 429:
    print("   ⚠️  Rate limited or quota exceeded")
else:
    print(f"   ❌ Error: {response.text}")

# Test 2: Try WebSocket-style header
print("\n2. Testing Realtime API header...")
headers["OpenAI-Beta"] = "realtime=v1"
response = requests.get("https://api.openai.com/v1/models", headers=headers)
print(f"   Status: {response.status_code}")

print("\n" + "="*50)
print("RECOMMENDATIONS:")
print("="*50)

if response.status_code == 401:
    print("• Get a new API key from: https://platform.openai.com/api-keys")
    print("• Make sure you're using an API key (starts with 'sk-'), not a session token")
elif response.status_code == 200 and not realtime_models:
    print("• Your key works but may not have Realtime API access")
    print("• Realtime API requires a paid account with tier 1+ access")
    print("• Check your usage tier: https://platform.openai.com/settings/organization/limits")
else:
    print("• API key appears valid")
    print("• If WebSocket still fails, check firewall/proxy settings")
    print("• Try disabling VPN if you're using one")
