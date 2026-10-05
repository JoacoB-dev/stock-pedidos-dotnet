[English version](README.en.md)

# Stock y Pedidos — ASP.NET Core 10 + EF Core + SQL Server + React

Sistema de **stock y pedidos para una distribuidora mayorista chica**: productos con SKU y stock mínimo, clientes, pedidos con renglones y un ciclo de estados, movimientos de stock auditables, alertas de stock bajo y reportes de ventas sacados de una vista y un stored procedure de SQL Server.

> **Proyecto de portfolio.** Lo hice para practicar y mostrar lo que piden los avisos de **Programador .NET Junior** y **Desarrollador backend .NET** (C#, ASP.NET Core, Entity Framework, SQL Server, React/Angular, Git, tests). No es un sistema de una empresa real: la distribuidora, los clientes, los productos y los CUIT son **datos de ejemplo**.
>
> Lo complementa otro proyecto .NET mío (Mesa de Reclamos, con Angular + PostgreSQL + SOAP). Este se enfoca en lo que aquel no tiene: **SQL Server, EF Core con migraciones, concurrencia optimista con `rowversion`, vista y stored procedure, y React**.

![Inicio con alertas de stock bajo](docs/capturas/1-dashboard.png)

## Qué hace

| Funcionalidad | Detalle |
|---|---|
| Productos | SKU único (`ALM-001`), categoría, precio, stock actual, stock mínimo, alta/baja lógica. Listado con búsqueda, filtro por categoría, "solo stock bajo" y paginación. |
| Clientes | Razón social, CUIT validado con dígito verificador, contacto. |
| Pedidos | Renglones (producto + cantidad + precio congelado al momento de cargarlo). Estados **Borrador → Confirmado → Preparado → Entregado**, o **Cancelado**. |
| Confirmar | Descuenta el stock de todos los renglones **en una sola transacción**. Si falta stock de algo, no descuenta nada y responde 409 con la lista de faltantes. Seguro ante dos confirmaciones al mismo tiempo (ver *Decisiones técnicas*). |
| Cancelar | Si el pedido ya había descontado stock, lo devuelve. Exige motivo. |
| Movimientos | Cada cambio de stock (ingreso, venta, cancelación, ajuste) queda registrado con usuario, fecha, cantidad, stock resultante y referencia al pedido. |
| Alertas | Dashboard con productos en o por debajo del mínimo, pedidos por estado y ventas del mes. |
| Reportes | Ventas por mes y producto (**vista** `vw_VentasPorMesProducto`) y resumen mensual con ticket promedio (**stored procedure** `sp_ResumenVentasMensual`), los dos consumidos desde EF Core. |
| Usuarios | JWT con roles **Vendedor** (carga y confirma sus pedidos, ve solo los suyos) y **Administrador** (todo: productos, ingresos/ajustes, preparar/entregar/cancelar confirmados, auditoría y reportes). |

### Usuarios demo

| Usuario | Contraseña | Rol |
|---|---|---|
| `admin@demo.local` | `demo1234` | Administrador |
| `vendedor@demo.local` | `demo1234` | Vendedor |

Los crea la semilla solo si `Bd:CargarUsuariosDemo` está en `true` (en Docker se controla con `CARGAR_DATOS_DEMO` del `.env`).

## Stack

- **Backend:** C# / .NET 10, ASP.NET Core 10 con controllers, FluentValidation, ProblemDetails (RFC 9457), OpenAPI + Swagger UI, autenticación JWT Bearer, `PasswordHasher` de ASP.NET Identity para las claves.
- **Datos:** SQL Server 2022, Entity Framework Core 10 (migraciones, `rowversion`, check constraints, índices, vista mapeada como entidad sin clave, `SqlQuery` para el stored procedure).
- **Frontend:** React 19 + TypeScript + Vite, React Router, CSS propio (sin librería de componentes). Se compila a `wwwroot` y lo sirve la misma API.
- **Tests:** xUnit (unitarios + integración con `WebApplicationFactory` contra **SQL Server real**), Vitest + Testing Library en el frontend.
- **Infra:** Dockerfile multi-etapa (Node → SDK → runtime), docker-compose con SQL Server, GitHub Actions.

### Estructura

```
src/
  Stock.Dominio/   Entidades y reglas (Pedido.Confirmar, Producto.AplicarMovimiento, Cuit…). Sin dependencias.
  Stock.Datos/     DbContext, configuraciones de EF, migraciones (incluye vista y SP).
  Stock.Api/       Controllers, DTOs + validadores, servicios (casos de uso), JWT, manejo de errores, semilla.
tests/Stock.Tests/ Unitarios/ e Integracion/
web/               React + TypeScript + Vite
```

Capas simples a propósito: las reglas de negocio están en el dominio (y se testean sin base), los servicios cargan/guardan y manejan transacción y concurrencia, y los controllers solo traducen HTTP.

## Cómo correrlo

### Opción 1: Docker (lo más fácil, incluido Windows 10 con Docker Desktop)

Requisitos: Docker Desktop (en Windows 10 con el backend WSL 2 activado) y unos 2 GB de RAM libres para SQL Server.

```bash
cp .env.example .env        # en PowerShell: Copy-Item .env.example .env
# editá .env: MSSQL_SA_PASSWORD y JWT_CLAVE (32+ caracteres)
docker compose up --build
```

- Aplicación: <http://localhost:8080> (interfaz React + API)
- Swagger: <http://localhost:8080/swagger>
- SQL Server desde tu máquina (SSMS / Azure Data Studio): `localhost,14330`, usuario `sa`, la clave del `.env`.

La API espera a que SQL Server esté sano, aplica las migraciones sola y carga los datos demo. Para empezar de cero: `docker compose down -v`.

> En Docker Desktop para Windows, si el puerto 8080 está ocupado cambiá `API_PUERTO_HOST` en el `.env`.

### Opción 2: sin Docker (Visual Studio / VS Code / Rider)

Requisitos: **.NET SDK 10**, **Node 22** y un **SQL Server** (2019/2022, Developer o Express; LocalDB no lo probé).

1. Configurá la conexión. `appsettings.Development.json` apunta a `localhost,1433`, base `stock_dev`, usuario `sa`, clave `Cambiar_Esta_Clave_2026`. Si la tuya es otra, pisala con una variable de entorno o con *user secrets*:
   ```bash
   # bash
   export ConnectionStrings__Stock="Server=localhost,1433;Database=stock_dev;User Id=sa;Password=TU_CLAVE;TrustServerCertificate=True"
   # PowerShell
   $env:ConnectionStrings__Stock="Server=localhost,1433;Database=stock_dev;User Id=sa;Password=TU_CLAVE;TrustServerCertificate=True"
   # con SQL Server Express y autenticación de Windows:
   # Server=.\SQLEXPRESS;Database=stock_dev;Trusted_Connection=True;TrustServerCertificate=True
   ```
   Si no tenés SQL Server instalado, podés levantar solo la base con Docker: `docker compose up -d sqlserver` (queda en el puerto 14330).
2. API (en desarrollo aplica migraciones y semilla al arrancar):
   ```bash
   dotnet run --project src/Stock.Api      # http://localhost:5080  y  /swagger
   ```
3. Frontend con recarga en caliente (Vite le pasa `/api` a la API en :5080):
   ```bash
   cd web
   npm install
   npm run dev                              # http://localhost:5173
   ```
   O `npm run build`, que deja la interfaz compilada en `src/Stock.Api/wwwroot` y la sirve la API directamente.

Migraciones a mano (opcional, la herramienta `dotnet-ef` está en el manifiesto `.config`):

```bash
dotnet tool restore
dotnet ef database update -p src/Stock.Datos -s src/Stock.Datos
dotnet ef migrations add NombreDelCambio -p src/Stock.Datos -s src/Stock.Datos -o Migraciones
```

## Pantallas

| | |
|---|---|
| ![Alta de pedido con renglones](docs/capturas/2-nuevo-pedido.png) | ![Listado de productos con filtros](docs/capturas/3-productos.png) |
| **Nuevo pedido**: cliente, renglones con buscador de productos, stock disponible y total en vivo. | **Productos**: búsqueda, categoría, "solo stock bajo", paginación. |
| ![Reportes](docs/capturas/4-reportes.png) | ![Vista en celular](docs/capturas/5-celular.png) |
| **Reportes**: resumen mensual (stored procedure) y ventas por producto (vista). | La interfaz se adapta al celular. |

Otras pantallas: login, listado de pedidos con filtros por estado/cliente/fechas, detalle de pedido con sus acciones según rol y estado, clientes, y auditoría de movimientos de stock (solo administrador).

## Endpoints principales

Todos bajo `/api`, con JWT salvo el login. Los errores salen en formato **ProblemDetails** (`application/problem+json`).

| Método | Ruta | Rol | Qué hace |
|---|---|---|---|
| POST | `/auth/login` | — | Devuelve el token, nombre y rol |
| GET | `/auth/yo` | cualquiera | Usuario del token |
| GET | `/productos?texto=&categoriaId=&soloStockBajo=&pagina=&tamanio=` | cualquiera | Listado paginado |
| GET/POST/PUT | `/productos`, `/productos/{id}` | Admin (escritura) | Alta y edición (el PUT lleva `rowVersion`) |
| POST | `/productos/{id}/movimientos` | Admin | Ingreso de mercadería o ajuste con motivo |
| GET/POST | `/categorias` | Admin (escritura) | Categorías |
| GET/POST/PUT | `/clientes`, `/clientes/{id}` | cualquiera | Clientes |
| GET/POST | `/pedidos?estado=&clienteId=&texto=&desde=&hasta=&pagina=` | cualquiera | El vendedor ve solo sus pedidos |
| PUT | `/pedidos/{id}` | cualquiera | Modificar un borrador |
| POST | `/pedidos/{id}/confirmar` | cualquiera | Descuenta stock (409 si falta) |
| POST | `/pedidos/{id}/preparar`, `/pedidos/{id}/entregar` | Admin | Avanza el estado |
| POST | `/pedidos/{id}/cancelar` | Vendedor (su borrador) / Admin | Devuelve stock si correspondía |
| GET | `/movimientos?productoId=&tipo=&pedidoId=&desde=&hasta=` | Admin | Auditoría de stock |
| GET | `/dashboard` | cualquiera | Stock bajo, pedidos por estado, ventas del mes |
| GET | `/reportes/ventas-por-producto?anio=&mes=&categoria=` | Admin | Lee la vista |
| GET | `/reportes/resumen-mensual?desde=&hasta=` | Admin | Ejecuta el stored procedure |
| GET | `/salud` | — | Healthcheck |

Códigos que usa: 400 (validación, con errores por campo), 401, 403, 404, 409 (sin stock o conflicto de concurrencia), 422 (regla de negocio: transición inválida, SKU duplicado, etc.).

## Decisiones técnicas

- **Confirmar un pedido es atómico y seguro ante concurrencia.** `Producto` y `Pedido` tienen una columna `rowversion`. Al confirmar, el servicio abre una transacción, carga pedido y productos, el dominio valida stock y descuenta, y EF genera `UPDATE … WHERE Id = @id AND RowVersion = @leida`. Si otro usuario tocó el mismo producto entre medio, ese UPDATE no afecta filas, EF lanza `DbUpdateConcurrencyException`, se hace rollback de **todo** (stock, estado y movimientos) y se reintenta leyendo los valores nuevos (hasta 3 veces). En el reintento puede resultar que ya no alcanza → 409. También se reintenta ante un deadlock de SQL Server (error 1205). Hay tests de integración que lanzan confirmaciones en paralelo y verifican que nunca se vende más de lo que hay ni se descuenta dos veces.
- **Concurrencia optimista también en la edición.** El PUT de producto recibe la `rowVersion` que el usuario vio; si cambió, 409 en vez de pisar el cambio de otro.
- **Defensa en la base además del código:** check constraints (`Stock >= 0`, `Cantidad > 0`, precio no negativo), índices únicos (SKU, CUIT, email, producto repetido en un pedido).
- **Precio congelado en el renglón:** si mañana cambia el precio de lista, los pedidos viejos no cambian su total.
- **Movimientos como registro de auditoría:** el stock nunca se toca "a mano"; siempre pasa por `Producto.AplicarMovimiento`, que genera el movimiento con el stock resultante. Un ajuste negativo que dejaría stock negativo se rechaza.
- **Vista y stored procedure dentro de una migración** (`migrationBuilder.Sql`), así quedan versionados junto al esquema y los tests los crean igual que en producción. La vista se mapea como entidad sin clave (`ToView`) y se le pueden aplicar filtros LINQ; el SP se llama con `Database.SqlQuery<T>` con parámetros interpolados (EF los convierte en parámetros, no hay inyección SQL).
- **Enums como texto** en la base (`Confirmado`, no `2`): se entiende sin mirar el código.
- **Fechas en UTC** con un `ValueConverter`, para que el JSON salga con `Z` y el navegador las muestre en hora local.
- **Errores uniformes:** un `IExceptionHandler` traduce las excepciones de dominio a ProblemDetails (404/409/422) y un filtro corre FluentValidation antes del controller (400 con errores por campo).
- **La API sirve el frontend compilado** (`wwwroot` + fallback a `index.html`): un solo contenedor, sin CORS.
- **Semilla con historia:** ~38 pedidos en los últimos meses, en distintos estados, para que reportes y dashboard tengan algo que mostrar desde el primer arranque.

## Tests

```bash
# Backend: 76 tests (unitarios + integración). Necesita un SQL Server; usa/crea la base stock_test.
export STOCK_TEST_BD="Server=localhost,1433;Database=stock_test;User Id=sa;Password=TU_CLAVE;TrustServerCertificate=True"
dotnet test

# Frontend: 9 tests con Vitest + Testing Library
cd web && npm test
```

En PowerShell: `$env:STOCK_TEST_BD="..."`. Si no definís la variable, usa `localhost,1433` con la clave del `.env.example`. Con `docker compose up -d sqlserver` podés apuntar a `localhost,14330`.

Qué cubren:

- **Unitarios (dominio y validadores):** matriz completa de transiciones de estado, confirmar sin stock no toca nada e informa todos los faltantes, cancelar devuelve stock, no se puede confirmar dos veces, total con precio congelado, ajustes que dejarían stock negativo, dígito verificador del CUIT, formato de SKU, validaciones de requests.
- **Integración (API completa con `WebApplicationFactory` + SQL Server real, base recreada con las migraciones):** login y 401/403 por rol, ciclo completo Borrador → Entregado, 409 con faltantes sin descontar nada, cancelación con movimientos auditables, el vendedor no ve pedidos ajenos, **confirmaciones simultáneas de pedidos que compiten por el mismo stock**, **el mismo pedido confirmado dos veces a la vez descuenta una sola vez**, edición con `rowversion` vieja → 409, la base rechaza stock negativo aunque se saltee el código, vista y stored procedure con datos conocidos, filtros y paginación, Swagger/OpenAPI responden, enums guardados como texto.

Resultado de la última corrida: **76/76 xUnit y 9/9 Vitest pasan.** CI (`.github/workflows/ci.yml`) corre lo mismo con SQL Server como servicio, verifica que no falten migraciones (`dotnet ef migrations has-pending-model-changes`), compila el frontend y construye la imagen Docker.

## Qué no incluye / qué no se probó

- **No hay** registro de usuarios ni recupero de contraseña (los usuarios se crean por semilla), ni refresh tokens: el JWT dura 8 horas.
- **No hay** proveedores ni órdenes de compra: los ingresos de mercadería se cargan como movimiento con un comentario.
- **No hay** facturación, impuestos, listas de precios por cliente ni descuentos.
- Los reportes son por mes calendario en **UTC** (un pedido confirmado el último día del mes a las 22 h de Argentina cae en el mes siguiente).
- **Probado:** tests en Linux contra SQL Server 2022 en contenedor; `docker compose up` con la imagen construida, login, alta → confirmar → cancelar pedido, reportes y Swagger contra el contenedor.
- **No probado:** Windows 10 con Docker Desktop (las instrucciones son las estándar, no las corrí en Windows), SQL Server Express/LocalDB, ni que el workflow de GitHub Actions corra en GitHub (no lo subí todavía). En el entorno donde lo armé, `docker compose build` necesitó agregar el certificado de un proxy de red para que `dotnet restore` bajara paquetes; en una máquina normal no hace falta y el Dockerfile del repo es el limpio.
- Sin pruebas de carga: la concurrencia está probada con 8 confirmaciones en paralelo sobre el mismo producto, no con miles de usuarios.

## Cómo contarlo en una entrevista

1. **Qué es:** un sistema de stock y pedidos para una distribuidora chica, hecho de punta a punta: API en ASP.NET Core 10, EF Core contra SQL Server, frontend en React con TypeScript, tests y Docker.
2. **El problema interesante es la concurrencia:** dos vendedores confirmando pedidos del mismo producto al mismo tiempo. Lo resolví con concurrencia optimista (`rowversion`) + transacción + reintento, y lo demostré con un test que confirma pedidos en paralelo.
3. **Reglas en el dominio:** `Pedido.Confirmar` valida transición y stock y devuelve los movimientos; eso se testea sin base. El servicio solo orquesta y guarda.
4. **SQL Server de verdad, no solo EF:** migraciones, check constraints, índices únicos, una vista mapeada en EF y un stored procedure llamado con `SqlQuery`, todo versionado en migraciones.
5. **API prolija:** validación con FluentValidation, errores en ProblemDetails con códigos coherentes (400/404/409/422), Swagger con JWT, roles.
6. **Auditoría:** todo cambio de stock queda como movimiento con usuario y stock resultante; cancelar devuelve el stock con su propio movimiento.
7. **Tests de integración contra SQL Server real** con `WebApplicationFactory`, no contra InMemory, porque InMemory no respeta `rowversion`, constraints ni transacciones.

**Preguntas probables**

- *¿Por qué concurrencia optimista y no un lock?* — Los conflictos son raros; no quería bloquear filas mientras alguien carga un pedido. Si hay conflicto, rollback y reintento. Un `UPDATE … SET Stock = Stock - @n WHERE Stock >= @n` también sirve; elegí `rowversion` porque EF lo maneja nativo y protege también la edición.
- *¿Qué pasa si falla a mitad de la confirmación?* — Todo va en una transacción: o se descuenta todo el stock, cambia el estado y se graban los movimientos, o no pasa nada.
- *¿Por qué no usaste InMemory para los tests?* — No soporta `rowversion`, transacciones ni la vista/SP; el test de concurrencia no probaría nada.
- *¿Cuándo usarías un stored procedure en vez de LINQ?* — Para reportes con agregaciones que conviene resolver en el motor o que comparte otro sistema (por ejemplo, un reporte de BI). Para el CRUD, LINQ.
- *¿Cómo manejás los errores?* — Excepciones de dominio tipadas → un `IExceptionHandler` las convierte en ProblemDetails; nunca se devuelve un stack trace.
- *¿Qué agregarías?* — Refresh tokens, proveedores y órdenes de compra, reportes por zona horaria local, y logs estructurados con Serilog.
- *¿Por qué React y no Angular?* — Mi otro proyecto .NET usa Angular; acá quise mostrar React con TypeScript, que también piden muchos avisos.

---

Autor: Joaquín Alejandro Bobbio — estudiante de la Licenciatura en Programación (UNAHUR). Proyecto de portfolio con datos de ejemplo.
