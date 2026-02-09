#!/bin/bash
# Cloudflare DDNS updater for snowcapsystems.com
# Updates multiple A records when public IP changes.
# Uses local IP cache to avoid unnecessary Cloudflare API calls.

CONFIG="/etc/cloudflare-ddns.conf"
LOG="/var/log/cloudflare-ddns.log"
IP_CACHE="/var/cache/cloudflare-ddns-ip"
CF_API="https://api.cloudflare.com/client/v4"

# A records to maintain (space-separated)
RECORDS="snowcapsystems.com lucyapi.snowcapsystems.com"

log() {
    echo "$(date '+%Y-%m-%d %H:%M:%S') $1" >> "$LOG"
}

# Load config
if [ ! -f "$CONFIG" ]; then
    log "ERROR: Config file $CONFIG not found"
    exit 1
fi
source "$CONFIG"

# Get current public IP
PUBLIC_IP=$(curl -s --max-time 10 ifconfig.me)
if [ -z "$PUBLIC_IP" ]; then
    log "ERROR: Failed to get public IP"
    exit 1
fi

# Check local cache first - skip Cloudflare API entirely if IP unchanged
if [ -f "$IP_CACHE" ]; then
    CACHED_IP=$(cat "$IP_CACHE")
    if [ "$PUBLIC_IP" = "$CACHED_IP" ]; then
        exit 0
    fi
    log "INFO: IP change detected ($CACHED_IP -> $PUBLIC_IP) - updating records"
fi

# Update each A record
for RECORD_NAME in $RECORDS; do

    # Get A record from Cloudflare (record ID + current IP)
    CF_RESPONSE=$(curl -s --max-time 10 -X GET \
        "$CF_API/zones/$CF_ZONE_ID/dns_records?type=A&name=$RECORD_NAME" \
        -H "Authorization: Bearer $CF_API_TOKEN" \
        -H "Content-Type: application/json")

    CF_SUCCESS=$(echo "$CF_RESPONSE" | grep -o '"success":true')
    if [ -z "$CF_SUCCESS" ]; then
        log "ERROR: Cloudflare API call failed for $RECORD_NAME: $CF_RESPONSE"
        continue
    fi

    RECORD_ID=$(echo "$CF_RESPONSE" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
    RECORD_IP=$(echo "$CF_RESPONSE" | grep -o '"content":"[^"]*"' | head -1 | cut -d'"' -f4)

    # No A record exists yet
    if [ -z "$RECORD_ID" ]; then
        log "INFO: No A record found for $RECORD_NAME - creating with IP $PUBLIC_IP"
        CREATE_RESPONSE=$(curl -s --max-time 10 -X POST \
            "$CF_API/zones/$CF_ZONE_ID/dns_records" \
            -H "Authorization: Bearer $CF_API_TOKEN" \
            -H "Content-Type: application/json" \
            --data "{\"type\":\"A\",\"name\":\"$RECORD_NAME\",\"content\":\"$PUBLIC_IP\",\"ttl\":300,\"proxied\":false}")
        CREATE_SUCCESS=$(echo "$CREATE_RESPONSE" | grep -o '"success":true')
        if [ -n "$CREATE_SUCCESS" ]; then
            log "INFO: A record created: $RECORD_NAME -> $PUBLIC_IP"
        else
            log "ERROR: Failed to create A record for $RECORD_NAME: $CREATE_RESPONSE"
        fi
        continue
    fi

    # IPs match - nothing to do for this record
    if [ "$PUBLIC_IP" = "$RECORD_IP" ]; then
        continue
    fi

    # IPs differ - update the record
    log "INFO: Updating $RECORD_NAME: $RECORD_IP -> $PUBLIC_IP"
    UPDATE_RESPONSE=$(curl -s --max-time 10 -X PUT \
        "$CF_API/zones/$CF_ZONE_ID/dns_records/$RECORD_ID" \
        -H "Authorization: Bearer $CF_API_TOKEN" \
        -H "Content-Type: application/json" \
        --data "{\"type\":\"A\",\"name\":\"$RECORD_NAME\",\"content\":\"$PUBLIC_IP\",\"ttl\":300,\"proxied\":false}")

    UPDATE_SUCCESS=$(echo "$UPDATE_RESPONSE" | grep -o '"success":true')
    if [ -n "$UPDATE_SUCCESS" ]; then
        log "INFO: A record updated: $RECORD_NAME -> $PUBLIC_IP"
    else
        log "ERROR: Failed to update A record for $RECORD_NAME: $UPDATE_RESPONSE"
    fi

done

# Update cache after processing all records
echo "$PUBLIC_IP" > "$IP_CACHE"
