

Özellikler:
	1. Verilen dosyadaki(.csv) kullanıcı bilgileri okunur.
	2. Veriler okunduktan sonra sınıf yapısına uygun olarak formatlanır.
	3. Verilerde yer alan kullanıcı bilgileri ile moodle üzerinde yeni kullanıcı açılır.
	4. Kullanıcı açıldıktan sonra gelen id' bilgisi veri ile eşlenir.
	5. Son kullanıcı bilgileri diske yazılır.

Features:
	1. The user information in the given file (.csv) is read.
	2. After the data is read, it is formatted in accordance with the class structure.
	3. A new user is created on moodle with the user information in the data.
	4. After the user is opened, the 'id' information is matched with the data.
	5. End user information is written to disk.

//users.csv
```CSV 

John CLOUD;password1;username@email.com
Jack JONAS;password2;username@email.com
Daniel POWELL;password3;username@email.com

```


Users in CSV are created as moodle users.

//formatedUsers.json
```JSON 
[
  {
    "id": null,
    "username": "password3",
    "password": "password3",
    "firstname": "Daniel",
    "lastname": "POWELL",
    "email": "username@email.com",
    "role": 5,
    "isCourseEnroll": false,
    "isAssignRole": false
  },
  ...
]
```

```JSON 
[
  {
    "id": "123213123123123", //successfully  created user on moodle 
    "username": "password1",
    "password": "password1",
    "firstname": "John",
    "lastname": "CLOUD",
    "email": "username@email.com",
    "role": 5,
    "isCourseEnroll": false,
    "isAssignRole": false
  },
  {
    "id": null, //not successfully  created user on moodle. try again!
    "username": "password3",
    "password": "password3",
    "firstname": "Daniel",
    "lastname": "POWELL",
    "email": "username@email.com",
    "role": 5,
    "isCourseEnroll": false,
    "isAssignRole": false
  },
  ...
]
```
 