<div align="center">

### 🇪🇸 **Español** &nbsp;·&nbsp; 🇬🇧 [English](README.md) &nbsp;·&nbsp; 🇮🇹 [Italiano](README.it.md)

🌐 Sitio oficial: **[calcio.dev/es](https://calcio.dev/es)**

</div>

---

# PC Calcio 7 Trainer

Un trainer gratuito para **PC Calcio 7** y **PC Calcio 7 Plus** (Dinamic Multimedia, 1998),
la edición italiana de **PC Fútbol 7**. Modifica tu partida mientras el juego está abierto:
dinero del club, capacidad del estadio, y atributos, edad, nacionalidad y moral de cualquier
jugador.

## En resumen

### ⬇ [**Descargar PcCalcio7Trainer.exe**](https://github.com/andreaolivato/pccalcio7-trainer/releases/latest/download/PcCalcio7Trainer.exe)

Un solo archivo. Nada que instalar. El sitio oficial del proyecto es
**[calcio.dev/es](https://calcio.dev/es)**.

1. Abre **PC Calcio 7** y carga tu partida
2. Ejecuta el programa: se conecta solo y detecta cuál es tu equipo
3. Cambia dinero, estadio o jugadores y pulsa **Aplicar**
4. Sal de la pantalla del juego y vuelve a entrar, luego **guarda dentro del juego**

Windows avisará de un "editor desconocido" y el antivirus puede protestar: el trainer escribe en
la memoria del juego, que es lo que hacen también los malware. El código fuente está aquí, por
si prefieres compilarlo tú.

> Proyecto independiente y no oficial, sin relación con Dinamic Multimedia ni con ningún
> titular de derechos. No incluye archivos del juego: necesitas tenerlo ya instalado.

![El trainer conectado a una partida](docs/screenshot.png)

> **¿Tienes PC Fútbol en vez de PC Calcio?** El trainer está hecho para la edición italiana y
> busca el proceso `MANAGCAL.EXE`. Las estructuras de memoria de la serie son muy parecidas,
> así que portarlo debería ser viable — [docs/METHODOLOGY.md](docs/METHODOLOGY.md) explica
> cómo, y las contribuciones son bienvenidas.

---

## Qué puedes cambiar

| | Detalle |
|---|---|
| **Dinero del club** | Hasta 900.000 miles de millones |
| **Capacidad del estadio** | De 100 a 1.000.000 de plazas |
| **Atributos** | Velocidad, Resistencia, Agresividad, Calidad, Juego de manos, Entradas, Pase, Regate, Remate, Tiro, Estado de forma |
| **Media** | No se edita directamente: el juego la calcula como promedio de Velocidad, Resistencia, Agresividad y Calidad, así que subir esas sube la Media |
| **Edad** | Cambiando el año de nacimiento — dura hasta que el juego recarga la carrera (nueva temporada o reinicio), porque las fechas de nacimiento se releen de la base de datos; basta con volver a aplicarla |
| **Nacionalidad** | A elegir entre 31 países confirmados. Es el campo que usa la regla de *extracomunitarios*: hacer italiano a un brasileño libera una plaza de extranjero — confirmado en el juego alineando un cuarto extracomunitario. Misma duración que la edad: vuelve a aplicarla tras recargar |
| **Moral** | De 23 a 99 |
| **Restaurar** | Devuelve a un jugador los valores que tenía antes de tus cambios |

Funciona con **tu** equipo y con **todos los demás**: 925, buscables por nombre.

### Lo que no hace, a propósito

**Traspasos.** Mover un jugador de un equipo a otro no está soportado. La plantilla se
construye desde una lista aparte, y meter a un jugador sin las estructuras que el juego crea
en un fichaje real hace que el juego se cierre. Si quieres un jugador, ponte dinero y fíchalo
desde el **director deportivo** del juego.

**Editar partidas guardadas.** Todo ocurre en la memoria del juego en ejecución.

---

## Requisitos

* Windows 8, 10 u 11 (o Windows 7 con .NET Framework 4 instalado)
* PC Calcio 7 o PC Calcio 7 Plus, instalado y abierto
* Nada más: ninguna descarga adicional

---

## Cómo se usa

1. Abre **PC Calcio 7** y carga tu partida.
2. Ejecuta **`PcCalcio7Trainer.exe`**. Se conecta solo y detecta cuál es tu equipo.
3. Cambia lo que quieras y pulsa **Aplicar**.
4. **Sal de la pantalla del juego y vuelve a entrar**, o el número seguirá siendo el viejo:
   el juego no redibuja una pantalla que ya está abierta.
5. **Guarda dentro del juego** para que los cambios sean permanentes.

Los cambios viven en la memoria del juego. Se mantienen al guardar y se pierden si recargas
sin guardar. Los archivos del juego nunca se modifican.

### Si no consigue conectarse

La ventana te dice cuál es el problema y qué hacer:

* **El juego no está abierto** → ábrelo, carga una partida y pulsa *Reintentar*.
* **El juego está abierto pero no hay partida cargada** → cárgala y pulsa *Reintentar*.
* **He encontrado el juego pero no puedo acceder a él** → iniciaste el juego como
  administrador, así que el trainer también lo necesita: ciérralo, clic derecho en el icono,
  *Ejecutar como administrador*.

---

## Qué descargar

Un solo archivo:

```
PcCalcio7Trainer.exe
```

Las traducciones están compiladas dentro del programa: no hay archivos de configuración ni
paquetes de idioma que copiar. `SelfTest.exe` es solo para diagnóstico y no hace falta.

El trainer **crea** tres archivos pequeños a su lado (`.club`, `.lang`, `.originals`) para
recordar el equipo elegido, el idioma y los valores originales de los jugadores. Puedes
borrarlos sin problema: solo pierdes las preferencias.

---

## Advertencias

**El antivirus y Windows van a protestar.** Un programa que escribe en la memoria de otro
programa se parece a un malware para un antivirus, y un archivo sin firmar descargado de
internet provoca el aviso de "editor desconocido". Es normal e inevitable sin un certificado
de firma. Si prefieres no fiarte del binario, el código está aquí y puedes compilarlo tú.

**Puede cerrar el juego.** Modificar memoria conlleva ese riesgo: pasó dos veces durante el
desarrollo. La versión actual es mucho más prudente, pero el riesgo no es cero. **Guarda tu
partida antes de usarlo.** Los archivos guardados nunca se tocan, así que como máximo pierdes
el progreso sin guardar.

**Valores absurdos dan resultados raros.** Un estadio de 200.000 plazas funciona, pero los
ingresos y la asistencia se calculan a partir de la capacidad, así que las pantallas económicas
pueden quedar poco creíbles.

---

## Documentación técnica

Esta versión es una guía rápida. El sitio oficial del proyecto es
**[calcio.dev/es](https://calcio.dev/es)**. La documentación completa está en inglés:

* **[README.md](README.md)** — versión completa, con cómo funciona y cómo compilarlo
* **[docs/MEMORY-MAP.md](docs/MEMORY-MAP.md)** — todos los campos encontrados en memoria, con
  el nivel de certeza de cada uno
* **[docs/METHODOLOGY.md](docs/METHODOLOGY.md)** — el método, para repetir este trabajo en
  otra versión del juego

## Contribuir

Traducciones, informes de fallos y correcciones son bienvenidos: ver
[CONTRIBUTING.md](CONTRIBUTING.md). Añadir un idioma es un archivo nuevo en `src/lang/`.

## Licencia

MIT — ver [LICENSE](LICENSE).
