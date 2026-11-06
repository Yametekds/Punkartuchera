namespace Punkartuchera.Core.Tutorials;

/// <summary>
/// Textos de la sección Tutoriales. Lo que cuentan del juego sale de las reglas básicas
/// ya cargadas en la app (<c>core.json</c>); cada paso enlaza al apartado del que sale.
/// </summary>
public static class TutorialCatalog
{
    public static IReadOnlyList<Tutorial> All { get; } =
    [
        App(),
        Play(),
        Band(),
        Terrain(),
    ];

    public static Tutorial? Find(string? id) => All.FirstOrDefault(t => t.Id == id);

    private static Tutorial App() => new(
        "app",
        "Cómo usar la app",
        "Manual para mataos con prisa",
        "Monta tu banda, consulta las reglas, pásale tu chusma al rival y lleva la partida sin papeles volando por la mesa.",
        [
            new("Instálala y olvídate del papel",
            [
                "Punkartuchera es una web que se instala como una app. Ábrela en el navegador del móvil y dale a «Añadir a pantalla de inicio» (o «Instalar»). Ya tienes el icono como cualquier otra app.",
                "Una vez cargada, tira sin conexión: bandas, reglas y mapa funcionan en el sótano más mugriento del páramo. Solo la partida con código necesita red.",
            ])
            {
                InApp = "Todo lo que guardas se queda en tu móvil, no en ninguna nube. Si borras los datos del navegador, tus bandas se van con ellos.",
            },
            new("El menú",
            [
                "Arriba a la izquierda tienes el botón ☰. Ahí está todo:",
                "• Bandas: tu chusma y las bandas rivales que te han pasado.",
                "• Reglas: el reglamento resumido, con buscador y todas las facciones.",
                "• Mapa: generador de mesas con la escenografía repartida.",
                "• Partida: el marcador para la bronca de hoy.",
                "• Tutoriales: esto que estás leyendo, por si se te olvida.",
            ]),
            new("Monta tu banda",
            [
                "En Bandas, dale a «Montar banda». Ponle nombre (sin nombre no sale, colega), elige facción y, si quieres, subfacción.",
                "Luego eliges cómo empezar: con una lista preconfigurada oficial, que te mete a toda la peña de golpe, o con la banda vacía para reclutar tú a cada miembro. Las listas oficiales son de la facción base: si eliges subfacción, toca reclutar a mano.",
            ])
            {
                Link = new("bandas/nueva", "Montar banda"),
            },
            new("Recluta y equipa a tu peña",
            [
                "Dentro de la banda, «Reclutar» abre la ficha de un miembro nuevo. Con «Sacar del reglamento» eliges tropa, personalidad o mercenario y la ficha se rellena sola con sus atributos.",
                "«Añadir equipo» y «Añadir habilidad» te enseñan solo lo que ese miembro puede comprar según su facción, con su precio. Las balas se suben y bajan con el contador.",
                "El coste de cada miniatura y el total de la banda se calculan solos. Tú preocúpate de no pasarte de puntos.",
                "Para la foto: cámara, galería, un retrato genérico o la foto oficial de la miniatura. Encuadra la jeta y listo.",
            ])
            {
                InApp = "En la pestaña de equipo de la banda, toca un arma o un trasto y te sale lo que dice el reglamento de él.",
            },
            new("Consulta las reglas sin pelearte con el PDF",
            [
                "En Reglas tienes las reglas básicas, las especiales, todas las armas y el equipo, y cada facción con sus tropas, restricciones y subfacciones.",
                "El buscador mira en todo el reglamento a la vez. Escribe «cobertura», «fuego defensivo» o lo que sea y salta directo.",
                "Cuando una regla especial sale en un texto, va subrayada: tócala y te lleva a su definición. Cada apartado lleva al PDF oficial y a su página, por si hay bronca de interpretación.",
            ])
            {
                Link = new("reglas", "Ir a las reglas"),
            },
            new("Pásale tu banda al rival",
            [
                "En el menú ⋮ de la banda, «Pasar al rival» enseña un QR y un botón para mandar el archivo .punkband por donde quieras (WhatsApp, Telegram, correo…).",
                "El rival la carga desde Bandas con «Cargar rival»: escanea el QR o abre el archivo. El QR no lleva fotos; el archivo sí.",
                "Lo que viaja: perfiles, equipo, habilidades y el coste total. Lo que NO viaja: tus balas. Son secretas, como manda el reglamento.",
                "La banda que te llega queda marcada como Rival y es de solo lectura: puedes cotillear, no tocar.",
            ]),
            new("Genera el mapa",
            [
                "En Mapa eliges la mesa (90×90 o 120×120), si el despliegue va arriba y abajo o a los lados, cuánta chatarra de cada tipo tienes y de qué material es. La app reparte la escenografía de forma equilibrada para los dos lados, a 10 cm entre piezas y de los bordes siempre que quepa.",
                "Cada mapa tiene un código. Mismo código y misma chatarra, mismo mapa: pásale el enlace al rival y veréis lo mismo.",
            ])
            {
                InApp = "Cada pieza sale con su color y su trama según el material, y con la marca del reglamento (2M, 3P…). Lo de siempre es la escenografía que pide el reglamento para esa mesa.",
                Link = new("mapa", "Generar un mapa"),
            },
            new("Lleva la partida",
            [
                "En Partida eliges tu banda y cómo jugar:",
                "• En este móvil: las dos bandas en el mismo teléfono. Vale cualquier banda, también las rivales que hayas cargado.",
                "• Cada uno en su móvil (si tu versión lo tiene montado): uno crea la partida y le pasa el código al otro, que lo mete en su Punkartuchera. Su banda te llega sola, sin balas ni fotos, y cada uno marca a los suyos.",
                "Durante la partida, marca a cada miembro como «Por actuar» o «Ya ha ido», y «En pie» o «Baja». El contador de Ronda de la app lleva los Turnos del reglamento: al pasar al siguiente, todos vuelven a estar por actuar.",
                "Cuando acabe la carnicería, «Terminar partida».",
            ])
            {
                Link = new("partida", "Ir a la partida"),
            },
        ]);

    private static Tutorial Play() => new(
        "jugar",
        "Cómo jugar",
        "Reglas para repartir leña",
        "Lo básico para echar tu primera partida: dados, turnos, acciones, tiros, hostias y cómo caen los tuyos.",
        [
            new("Lo que necesitas",
            [
                "Dos bandas montadas con los mismos puntos, una mesa con mucha chatarra encima, una cinta métrica en centímetros y dados de diez caras (d10). Muchos dados. Se pierden.",
                "Se puede medir cualquier distancia en cualquier momento. Entre miniaturas se mide de borde a borde de las peanas, y para las reglas cuenta la peana, no lo chula que sea la miniatura.",
                "Cómo se gana y cómo se despliega lo marca el escenario que juguéis: lo tenéis en el PDF oficial de Escenarios.",
            ])
            {
                RuleIds = ["medidas"],
                Images =
                [
                    new("medidas.webp", "Dos miniaturas sobre la mesa con flechas entre sus peanas", "Entre dos miniaturas se mide siempre de borde a borde de las peanas, por el lado más cercano. Ni nariz, ni lanza, ni pluma del casco: peana.", 4),
                ],
            },
            new("Los Atributos",
            [
                "Cada miniatura tiene siete Atributos. Van de 1 a 10 (Acciones de 1 a 3) y cuanto más alto, mejor:",
                "• A, Acciones: cuántas cosas hace en su turno.",
                "• C, Combate: hostias cuerpo a cuerpo.",
                "• P, Precisión: puntería.",
                "• AG, Agilidad: reflejos. Decide cuándo actúa.",
                "• F, Fuerza: lo bestia que pega.",
                "• D, Dureza: lo que aguanta.",
                "• T, Técnica: maña con máquinas y cacharros.",
                "Si la Agilidad, la Fuerza o la Dureza bajan para siempre a 0, la miniatura está Abatida.",
            ])
            {
                RuleIds = ["atributos"],
            },
            new("Las tiradas",
            [
                "Tirada de Atributo: tira 1d10 y saca igual o menos que el Atributo (con sus modificadores). Si con modificadores queda en 0 o menos, fallas sin tirar; si llega a 10, pasas sin tirar.",
                "Tirada enfrentada: cada uno tira 1d10 y suma lo suyo. Gana el más alto. En empate gana quien está activando, salvo en Combate, donde os dais los dos.",
                "Cada jugador puede repetir una sola tirada por Acción, y ninguna tirada se repite dos veces. Te quedas con lo último que salga, aunque sea peor.",
            ])
            {
                RuleIds = ["tiradas", "modificadores"],
            },
            new("Turnos y Rondas de Agilidad",
            [
                "La partida suele durar 6 Turnos. En cada Turno actúa todo el mundo, por Rondas de Agilidad: primero los más rápidos, de mayor a menor.",
                "Si en una Ronda hay miniaturas de los dos bandos, cada jugador tira 1d10 y el que gana decide quién empieza. Después os vais alternando miniatura a miniatura.",
                "Cada miniatura tiene una Fase de activación por Turno: hace todas sus Acciones y hasta que no termina no se activa otra.",
            ])
            {
                RuleIds = ["turno"],
                InApp = "En Partida, marca «Ya ha ido» a cada miniatura que termine su Fase de activación y no te liarás.",
            },
            new("Las Acciones",
            [
                "Una miniatura hace tantas Acciones como su Atributo A. Puedes repetirlas y no estás obligado a gastarlas todas.",
                "• Mover, Disparar, Apuntar, Asaltar, Combatir, Separarse del Combate, Fuego defensivo, Rapiñar un cadáver e Intercambiar balas.",
                "Apuntar da +3 a Precisión en los siguientes disparos al mismo objetivo en esa misma Fase de activación. Fuego defensivo te deja con el dedo en el gatillo para disparar (con -2) a un enemigo que se mueva en tu Línea de visión durante el resto del Turno, pero tiene que ser tu última Acción.",
            ])
            {
                RuleIds = ["acciones", "apuntar", "fuego-defensivo"],
            },
            new("Moverse",
            [
                "Cada Acción de Movimiento son 10 cm en terreno normal (Terreno chachi). El terreno chungo, la escalada, los saltos y las caídas tienen sus reglas: las tienes en el tutorial de terreno.",
                "Los aliados no estorban al mover, pero nadie acaba encima de otro, y para pasar por un hueco tiene que caber la peana.",
            ])
            {
                RuleIds = ["movimiento"],
            },
            new("Disparar",
            [
                "Gasta una Acción. No puedes tener enemigos en contacto y tienes que ver al objetivo. Supera una tirada de Precisión con estos modificadores:",
            ])
            {
                TableFrom = "disparar",
                RuleIds = ["disparar", "distancias", "disparar-combate"],
                Images =
                [
                    new("linea-vision.webp", "Un tirador frente a cuatro objetivos marcados A, B, C y D, uno tras una caseta y otro tras unos bidones", "Línea de visión del Cardenal Nuclear. A: está fuera de su arco frontal, no lo ve. B: delante y sin nada en medio, lo ve. C: detrás de una Cobertura de tamaño 2 con peana de 25 mm, tapado del todo, no lo ve. D: detrás de algo de tamaño 1, que no tapa entera una peana de 25 mm, así que sí lo ve.", 5),
                ],
                InApp = "Las armas de fuego gastan balas. En Reglas › Armas y equipo tienes el alcance corto, medio y largo de cada arma.",
            },
            new("A hostias: Asaltar y Combatir",
            [
                "La única forma de ponerse en contacto con un enemigo es Asaltarlo: mover hasta tocarlo. El Asalto incluye un Combate gratis y te da +1, y otro +1 por cada Movimiento seguido que llevabas viéndolo y avanzando hacia él.",
                "Combatir: los dos tiráis 1d10 + Combate + arma + lo que toque. El más alto Impacta al otro. En empate, cobráis los dos.",
                "Si hay más gente metida, cada aliado en el Combate da apoyo: +1 si tiene Combate 5 o menos, +2 si tiene 6 o más.",
                "Para salir corriendo: Separarse del Combate, tirada enfrentada de Agilidad contra todos los que te tocan. Si pierdes, cada uno que te gane te Impacta gratis.",
            ])
            {
                RuleIds = ["asaltar", "combatir", "combate-multiple", "separarse"],
                Images =
                [
                    new("asalto.webp", "Un Bruto avanza en dos movimientos hacia una Mutante", "Un Bruto de los Pandilleros gasta dos Acciones de Movimiento para llegar a la peana de una Mutante. Como la veía al empezar las dos, cada una le da +1: entra en Combate con +2.", 12),
                    new("combate-multiple.webp", "Seis miniaturas en un Combate múltiple, marcadas de A a F", "Combate múltiple: los Pandilleros A, B y C y los Mutardos D y E están metidos porque cada uno toca al menos a un enemigo. F solo toca a uno de los suyos, así que se queda mirando.", 14),
                ],
            },
            new("Impactos: Fuerza contra Dureza",
            [
                "Que te Impacten no es caer. El que pega tira 1d10 + Fuerza del Impacto; el que recibe, 1d10 + Dureza + Blindaje. Si gana el que pega, la víctima queda Abatida.",
                "El Blindaje sale de la armadura y de la Cobertura. La Penetración del arma se lo come: Blindaje 3 contra Penetración 2 deja solo +1.",
            ])
            {
                RuleIds = ["impactar", "blindaje"],
            },
            new("Abatidos, rapiña y balas",
            [
                "Una miniatura Abatida está fuera. Deja un marcador en su sitio: no tapa la vista ni estorba al mover.",
                "Con Rapiñar un cadáver (1 Acción en contacto) le quitas a un Abatido, amigo o enemigo, un arma o equipo especial. En el páramo no se tira nada.",
                "Las balas son secretas: apúntalas y al final el rival lo puede comprobar. Si te quedas seco, un aliado en contacto te puede pasar balas.",
            ])
            {
                RuleIds = ["abatidas", "rapinar"],
                InApp = "En Partida, marca «Baja» a los que caen y verás de un vistazo cuántos te quedan en pie.",
            },
        ]);

    private static Tutorial Band() => new(
        "banda",
        "Cómo armar tu banda",
        "Recluta a la peor calaña",
        "Puntos, facción, tropas, armas y trastos: cómo montar una banda legal según el reglamento sin que el rival te pille en un renuncio.",
        [
            new("Pactad los puntos",
            [
                "Antes de nada, poneos de acuerdo en el límite de puntos. Una partida estándar va de 350 a 500 puntos. Cada tropa cuesta lo suyo y se le suma lo que le compres; el total no puede pasarse nunca del límite.",
                "Tampoco vale meter a todo el poblado: máximo 3 miniaturas por cada fracción de 100 puntos.",
            ])
            {
                TableFrom = "puntos",
                RuleIds = ["puntos", "fracciones"],
            },
            new("Elige facción (y subfacción)",
            [
                "Cada facción juega a su manera: sus tropas, sus armas, su equipo y sus reglas. Échale un ojo a su resumen y a sus tropas antes de decidir con quién te manchas las manos.",
                "Muchas facciones tienen subfacciones: variantes con reglas propias que cambian lo que puedes llevar. Es opcional.",
                "Algunas facciones están en beta: se pueden jugar, pero sus reglas aún pueden cambiar.",
            ])
            {
                Link = new("reglas", "Ver las facciones"),
            },
            new("Respeta las Restricciones",
            [
                "Cada facción tiene sus Restricciones al montar la banda. Las típicas son cosas como un solo Jefe, como mucho una Personalidad o Mercenario, o que no haya más carne de cañón que el resto de la banda junta.",
                "Las de tu facción las tienes en su ficha del reglamento. Léelas antes de reclutar, que luego vienen los lloros.",
            ])
            {
                RuleIds = ["restricciones"],
            },
            new("Recluta tropas",
            [
                "Cada tropa tiene su coste, sus Atributos, su tamaño de peana y sus reglas especiales. Su ficha te dice también qué armas puede llevar (por ejemplo, «al menos una y como máximo dos») y con qué armadura viene.",
                "Las mejoras de armadura sustituyen a la que trae de serie, no se suman. Y nunca dos armas A dos manos.",
                "Las Personalidades y los Mercenarios son tipos con nombre y equipo propio. Pegan fuerte, pero cuentan para las Restricciones.",
            ])
            {
                RuleIds = ["armas-equipo"],
                InApp = "Con «Sacar del reglamento» la ficha se rellena con su perfil, y «Añadir equipo» solo te ofrece lo que esa tropa puede comprar.",
            },
            new("Compra equipo y balas",
            [
                "Además de lo de su facción, hay equipo que puede llevar cualquiera:",
                "• Balas: 15 puntos cada una, solo para armas de fuego. Las armas que no son de fuego tienen munición para toda la partida.",
                "• Bayoneta: +2 a la Penetración en cuerpo a cuerpo para un arma de fuego.",
                "• Escudo: más Blindaje en cuerpo a cuerpo y Cobertura +2 contra disparos. No va con armas A dos manos.",
                "• Protección contra la contaminación: pasas del Terreno contaminado.",
                "• Gato parameño: uno por banda y con su miniatura a la vista. Una vez por partida repites una tirada tuya u obligas al rival a repetir una suya.",
                "Las balas son caras. Pocas y bien puestas.",
            ])
            {
                Link = new("reglas/equipo", "Ver armas y equipo"),
            },
            new("Lo que se ve y lo que no",
            [
                "El equipo tiene que verse en la miniatura o dejarse claro. El rival tiene derecho a conocer perfiles, reglas, habilidades y mutaciones.",
                "Solo es secreto cuántas balas lleva cada uno y las miniaturas que entran más tarde (por ejemplo, con Infiltración).",
                "Puedes usar miniaturas de otras marcas, también en torneos oficiales. Lo que cuenta es la peana que pide la ficha.",
            ])
            {
                RuleIds = ["restricciones"],
                InApp = "Al pasarle tu banda al rival, la app quita las balas y solo manda el coste total, así que no hay forma de que te las cotillee.",
            },
            new("Atajo: las listas oficiales",
            [
                "Si no quieres comerte la cabeza, cada facción tiene listas preconfiguradas oficiales: una banda montada y lista para salir a pegarse.",
                "Al crear la banda en la app, elige «Lista preconfigurada» en «Empezar con» y se recluta todo de golpe. Luego puedes cambiar lo que quieras.",
            ])
            {
                Link = new("bandas/nueva", "Montar banda"),
            },
        ]);

    private static Tutorial Terrain() => new(
        "terreno",
        "Terreno y coberturas",
        "Escóndete o muere",
        "Cómo se mueve uno entre la chatarra, qué material para qué balas y cuándo un muro te salva el pellejo.",
        [
            new("Tipos de terreno",
            [
                "Antes de jugar, decidid qué es cada zona de la mesa:",
                "• Terreno chachi: el normal. 10 cm por Acción.",
                "• Terreno chungo (agua poco profunda, fango, escombros, cuestas): tirada de Agilidad para mover normal; si fallas o no tiras, solo la mitad.",
                "• Terreno pila chungo: solo la mitad. Puedes intentar moverte entero con Agilidad, pero si fallas te comes una Caída y quedas Tumbada.",
                "• Terreno impasable: ni se mueve ni se trepa. Si alguien acaba ahí, fuera de la mesa.",
                "• Terreno contaminado: si acabas tu Fase de activación encima, pierdes 1 de Dureza para siempre. La protección contra la contaminación te libra.",
            ])
            {
                RuleIds = ["terrenos"],
            },
            new("Subir, saltar y trepar",
            [
                "A la escenografía de tamaño 1 se sube sin gastar Movimiento; las peanas de más de 40 mm suben también a la de tamaño 2. Si no, rodea o trepa.",
                "Saltar es en horizontal y nunca hacia arriba. Hasta 2 veces la peana, sin tirada; de 2 a 3, tirada de Agilidad; de 3 a 4, Agilidad con -2; más, ni lo intentes.",
                "Trepar por una pared es como Terreno chungo; por una escalerilla, como Terreno chachi.",
            ])
            {
                RuleIds = ["obstaculos", "saltar", "trepar"],
                Images =
                [
                    new("obstaculo-estrecho.webp", "Una miniatura salta una valla estrecha de tamaño 1", "Valla de tamaño 1 y más estrecha que la peana (25 mm): la Pachacha la cruza y aparece al otro lado, gastando solo lo que avanza en horizontal.", 10),
                    new("obstaculo-ancho.webp", "Una miniatura se sube encima de un coche", "Coche de tamaño 1 pero más ancho que la peana: la Pachacha se queda encima. Si el coche cuenta como Terreno chungo o pila chungo, se aplica mientras esté ahí arriba.", 10),
                ],
            },
            new("Caídas",
            [
                "Dejarse caer hasta 2 veces la peana de altura es gratis. Más alto, Caída: tirada enfrentada de tu Dureza (sin Blindaje) contra la Fuerza de la Caída. Si la pasas, quedas Tumbada; si no, Abatida.",
            ])
            {
                TableFrom = "caidas",
                RuleIds = ["caidas"],
            },
            new("Tamaño de la Cobertura",
            [
                "Cada elemento de escenografía tiene un tamaño según lo alto que es:",
                "• Tamaño 1: hasta unos 3 cm.",
                "• Tamaño 2: entre 3 y 6 cm.",
                "• Tamaño 3: más de 6 cm.",
                "El tamaño decide a quién tapa. Un tamaño 1 protege a peanas de 25 y 40 mm, pero a las más grandes no. Un tamaño 2 protege a todas, y si las tapa del todo oculta a las de 25 y 40 mm. Un tamaño 3 que tape del todo oculta a cualquiera.",
            ])
            {
                RuleIds = ["coberturas", "aplicar-cobertura"],
            },
            new("Materiales y grado de Cobertura",
            [
                "El material decide cuánto aguanta. El grado de la Cobertura se suma al Blindaje del que está detrás:",
                "• Ligera (+3): vegetación densa, madera, neumáticos, sacos.",
                "• Media (+5): adobe, ladrillo, chapa fina, escombros.",
                "• Pesada (+7): vehículos, chapa gruesa, hormigón, piedra.",
                "Ojo, que la Penetración del arma también se come el Blindaje de la Cobertura. Contra un arma que perfora, la chapa fina es papel de fumar.",
            ])
            {
                RuleIds = ["coberturas", "blindaje"],
                Images =
                [
                    new("cobertura-coche.webp", "Un tirador del V Reich apunta a dos Mutardos, A junto a un coche y B al descubierto", "El del V Reich apunta a dos Mutardos. Al A alguna línea le cruza el coche: Cobertura parcial, y como un coche es Pesada, +7 al Blindaje. Al B no le tapa nada: a pelo.", 20),
                ],
            },
            new("Cobertura parcial o total",
            [
                "Traza líneas desde el frente del tirador hasta la peana del objetivo. Si alguna atraviesa escenografía, hay Cobertura. Si además alguna se libra, es parcial; si no se libra ninguna, es total.",
                "Si la Cobertura oculta al objetivo, no hay Línea de visión en ningún sentido: ni tú le ves ni él te ve.",
                "Si el tirador está pegado al elemento que le cubre a medias, ese elemento no protege a su objetivo. Asomarse tiene premio.",
                "En el 90 % de los casos se ve a ojo. Solo saca la regla en los casos muy justos.",
            ])
            {
                RuleIds = ["cobertura-parcial-total", "aplicar-cobertura"],
                Images =
                [
                    new("cobertura-parcial-total.webp", "Tres viñetas vistas desde arriba, A, B y C, con dos miniaturas a cada lado de un obstáculo", "Visto desde arriba. A: todas las líneas entre peanas cruzan el trasto, Cobertura total; como es de tamaño 2 y las peanas son de 25 mm, ni se ven. B: unas líneas lo cruzan y otras no, Cobertura parcial. C: ninguna línea pasa por el trasto, así que no cubre a nadie.", 19),
                ],
            },
            new("Evitar, apilar y las alturas",
            [
                "Evitar Cobertura: contra una Cobertura parcial puedes apuntar a lo que asoma. Pierdes -3 a Precisión, pero la Cobertura no suma Blindaje. Contra Coberturas pesadas o con armas de poca Penetración suele merecer la pena.",
                "Apilar: si el disparo cruza varias Coberturas, se usa la de mayor grado y +1 por cada una extra. Muro de ladrillo (+5) y escudo (+2) dan +6.",
                "Alturas: hay diferente altura si os separan más de 5 cm. Si el objetivo está arriba y pegado al borde, no tiene Cobertura; a menos de su peana del borde, sí; más adentro, está oculto.",
            ])
            {
                RuleIds = ["disparar-combate", "apilar-coberturas", "alturas"],
                Images =
                [
                    new("apilar-coberturas.webp", "Un tirador junto a unos neumáticos dispara hacia un objetivo tras otros neumáticos y un coche", "Apilar: el Chispa está pegado a los neumáticos A, así que esos no cuentan. El objetivo se cubre con los neumáticos B (Ligera) y el coche C (Pesada): se coge la mejor, +7, y +1 por la otra.", 21),
                    new("alturas.webp", "Tres Pandilleros, A, B y C, en lo alto de un edificio, y un enemigo abajo", "Alturas: A y C están a menos de una peana del borde, así que pueden disparar abajo. B está más adentro: ni dispara ni le pueden disparar. A, pegado al borde, no tiene Cobertura si le disparan desde el suelo.", 22),
                ],
            },
            new("Carne de cañón: miniaturas como Cobertura",
            [
                "Las peanas de otras miniaturas también cuentan como escenografía para ver si hay Cobertura. Si la tapan del todo, no puedes elegir a ese objetivo.",
                "Si la tapan a medias, se dispara como a un Combate: -3, y si fallas por culpa de ese -3, te comes al que estorba más cerca. Esta Cobertura no se puede Evitar.",
                "Hay armas a las que les da igual dónde te escondas: las de Plantilla ignoran Cobertura, y el fuego de las Incendiaria también. Con Postas o Tiro parabólico no se puede Evitar Cobertura, y contra quien tiene Camuflaje, Evitarla cuesta aún más.",
            ])
            {
                RuleIds = ["miniaturas-cobertura"],
                Images =
                [
                    new("miniaturas-cobertura.webp", "Una arquera dispara a un Mutardo B que está medio tapado por otro Mutardo A", "El Mutardo A tapa a medias al B: se dispara como a un Combate, con -3. Si fallas por ese -3, la flecha se la come A.", 21),
                ],
                Link = new("reglas/especiales", "Ver reglas especiales"),
            },
            new("Prepara la mesa",
            [
                "Antes de desplegar, repasad cada elemento y decidid su tamaño y su grado. El truco del reglamento: marcarlo debajo, por ejemplo 2M (tamaño 2, Media), 1P o 3L.",
                "Usa los materiales para decidir: una barricada de sacos y neumáticos es Ligera, una ruina de ladrillo es Media y un coche calcinado es Pesada. El charco tóxico no cubre, pero pactad si es Terreno contaminado.",
            ])
            {
                RuleIds = ["coberturas"],
                InApp = "En el generador de Mapa eliges el material de cada tipo de pieza y el plano ya sale con la marca de cada una. Si vuestra escenografía es distinta, cambiad el material y listo.",
                Link = new("mapa", "Generar un mapa"),
            },
        ]);
}
