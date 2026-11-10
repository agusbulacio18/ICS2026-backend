## TR4 – Configuración de servidores de Azure y Base de datos SQL

### Objetivo

El objetivo de esta tarea fue preparar la infraestructura en la nube donde va a vivir nuestra API: un servidor web (Azure App Service) y una base de datos (Azure SQL Database), ambos bajo el plan gratuito de Azure, para no generar ningún costo. Además, nos aseguramos de que la conexión entre la API y la base de datos quedara bien configurada y protegida, sin exponer contraseñas en el código.

### La base de datos (Azure SQL Database, plan gratuito)

Creamos un servidor de base de datos en Azure y, dentro de él, una base de datos bajo la oferta **Free**, que da un uso mensual gratuito sin necesidad de pagar nada.

Para que la API pudiera conectarse a esta base, tuvimos que habilitar el acceso desde internet al servidor y después agregar una regla especial de firewall que le permite a los demás servicios de Azure (como nuestro servidor web) conectarse sin problemas. Sin esta regla, la base de datos directamente rechaza cualquier conexión, aunque la contraseña esté bien.

### El servidor web (Azure App Service, plan Free F1)

Creamos el App Service, que es básicamente el "servidor" donde corre nuestra API, usando el plan **F1 (Free)**, que es el nivel gratuito de Azure. Lo configuramos para que funcione como un contenedor Docker, usando el mismo puerto (8080) que definimos en el Dockerfile, y le indicamos cuál es el comando que tiene que ejecutar para levantar la aplicación.

### Conectar la API con la base de datos, de forma segura

Acá estaba el detalle más importante: la API necesita saber cómo conectarse a la base de datos, pero esa información (usuario, contraseña, dirección del servidor) nunca debe quedar escrita en el código que subimos a GitHub, porque cualquiera que vea el repositorio la vería también.

Para resolver esto, cargamos la cadena de conexión directamente en la configuración del App Service, en Azure, como una variable de entorno. De esta manera, la aplicación la lee en el momento en que arranca, pero esa información nunca viaja dentro del código ni queda guardada en GitHub.

### Dejar todo listo para el pipeline automático

Por último, generamos las credenciales necesarias para que GitHub Actions (nuestro pipeline de CI/CD) pueda desplegar automáticamente los cambios en este servidor cada vez que se hace un push, sin que nadie tenga que entrar manualmente a Azure a actualizar nada.

### Cierre

Con esta tarea dejamos funcionando la infraestructura completa en la nube, sin ningún costo: la base de datos quedó accesible para nuestra API (CA1), el servidor web quedó andando en el plan gratuito (CA2), y la conexión entre ambos quedó configurada de forma segura, sin contraseñas expuestas en el código (CA3). Con esto, ya quedó todo preparado para que el pipeline de GitHub Actions (TR5) pueda desplegar la API automáticamente.
