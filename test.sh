./out/jubeka-cli env create --name TestEnv

./out/jubeka-cli env set --name TestEnv

./out/jubeka-cli request add --req-name dogs --method GET --url https://dog.ceo/api/breeds/list/all

./out/jubeka-cli request list

./out/jubeka-cli request edit --inline --req-name dogs --method POST

./out/jubeka-cli request list

./out/jubeka-cli request edit --inline --req-name dogs --method GET

./out/jubeka-cli request exec --req-name dogs