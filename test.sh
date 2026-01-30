#!/usr/bin/env bash
set -euo pipefail

CLI="./out/jubeka-cli"
TMP_DIR="$(mktemp -d)"
ENV_NAME="TestEnv-$RANDOM"

cleanup() {
	rm -rf "$TMP_DIR"
}
trap cleanup EXIT

assert_contains() {
	local output="$1"
	local expected="$2"
	if ! grep -Fq "$expected" <<<"$output"; then
		echo "Expected output to contain: $expected" >&2
		echo "Actual output:" >&2
		echo "$output" >&2
		exit 1
	fi
	if [[ -n "${current_test:-}" ]]; then
		echo "running $current_test"
	else
		echo "running"
	fi
	echo "PASS: $expected"
}

run_cmd() {
	local label="$1"
	shift
	local output
	current_test="$label"
	output=$("$@" 2>&1)
	echo "[$label]"
	echo "$output"
	echo
	return_output="$output"
}

cat >"$TMP_DIR/vars.yml" <<'YAML'
variables:
	token: abc
YAML

cat >"$TMP_DIR/openapi.yaml" <<'YAML'
openapi: 3.0.1
info:
	title: Dog API
	version: '1.0'
servers:
	- url: https://dog.ceo/api
paths:
	/breeds/list/all:
		get:
			operationId: listBreeds
			responses:
				'200':
					description: ok
YAML

run_cmd "env create" "$CLI" env create --name "$ENV_NAME" --vars "$TMP_DIR/vars.yml" --spec-file "$TMP_DIR/openapi.yaml"
assert_contains "$return_output" "Environment '$ENV_NAME' created."

run_cmd "env set" "$CLI" env set --name "$ENV_NAME"
assert_contains "$return_output" "Current environment set to '$ENV_NAME'."

run_cmd "env edit inline" "$CLI" env edit --name "$ENV_NAME" --inline --vars "$TMP_DIR/vars.yml"
assert_contains "$return_output" "Environment '$ENV_NAME' updated."

run_cmd "request add" "$CLI" request add --req-name dogs --method GET --url https://dog.ceo/api/breeds/list/all
assert_contains "$return_output" "Request 'dogs' added to '$ENV_NAME'."

run_cmd "request list (GET)" "$CLI" request list
assert_contains "$return_output" "dogs [GET] https://dog.ceo/api/breeds/list/all"

run_cmd "request edit inline (POST)" "$CLI" request edit --inline --req-name dogs --method POST
assert_contains "$return_output" "Request 'dogs' updated in '$ENV_NAME'."

run_cmd "request list (POST)" "$CLI" request list
assert_contains "$return_output" "dogs [POST] https://dog.ceo/api/breeds/list/all"

run_cmd "request edit inline (GET)" "$CLI" request edit --inline --req-name dogs --method GET
assert_contains "$return_output" "Request 'dogs' updated in '$ENV_NAME'."

run_cmd "request exec" "$CLI" request exec --req-name dogs
assert_contains "$return_output" "HTTP 200"
assert_contains "$return_output" "message"
assert_contains "$return_output" "status"

run_cmd "basic request" "$CLI" request --method GET --url https://dog.ceo/api/breeds/list/all --env "$TMP_DIR/vars.yml" --timeout 30
assert_contains "$return_output" "HTTP 200"

run_cmd "openapi request" "$CLI" openapi request --operation listBreeds --spec-file "$TMP_DIR/openapi.yaml" --env "$TMP_DIR/vars.yml" --timeout 30
assert_contains "$return_output" "HTTP 200"

run_cmd "help" "$CLI" -h
assert_contains "$return_output" "request add"
assert_contains "$return_output" "request edit"
assert_contains "$return_output" "request exec"
assert_contains "$return_output" "env edit"