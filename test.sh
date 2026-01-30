./out/jubeka-cli env create --name TestEnv

./out/jubeka-cli env set --name TestEnv

./out/jubeka-cli env request add --req-name dogs --method GET --url https://dog.ceo/api/breeds/list/all

./out/jubeka-cli env request list

./out/jubeka-cli env request edit --req-name dogs --method POST

./out/jubeka-cli env request list

./out/jubeka-cli env request edit --req-name dogs --method GET

./out/jubeka-cli -h