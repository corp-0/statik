#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."
umask 077
mkdir -p .certs

if [[ ! -f .certs/statik-dev-ca.key ]]; then
    openssl req -x509 -newkey rsa:3072 -nodes -days 730 \
        -keyout .certs/statik-dev-ca.key -out .certs/statik-dev-ca.crt \
        -subj '/CN=Statik local development CA' \
        -addext 'basicConstraints=critical,CA:TRUE,pathlen:0' \
        -addext 'keyUsage=critical,keyCertSign,cRLSign'
fi

openssl req -new -newkey rsa:2048 -nodes \
    -keyout .certs/statik-dev.key -out .certs/statik-dev.csr \
    -subj '/CN=statik.dev'

cat > .certs/statik-dev.ext <<'EOF'
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
subjectAltName=DNS:statik.dev,DNS:app.statik.dev
EOF

openssl x509 -req -in .certs/statik-dev.csr \
    -CA .certs/statik-dev-ca.crt -CAkey .certs/statik-dev-ca.key -CAcreateserial \
    -days 365 -extfile .certs/statik-dev.ext -out .certs/statik-dev.crt

echo 'Certificates created in .certs/. Trust statik-dev-ca.crt locally before opening the .dev hosts.'
