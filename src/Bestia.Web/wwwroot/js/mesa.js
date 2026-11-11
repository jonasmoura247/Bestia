(function () {
  'use strict';

  var app = document.getElementById('mesa-app');
  if (!app) { return; }

  var NAIPES = ['♥', '♦', '♠', '♣'];
  var VALORES = ['4', '5', '6', '7', '8', '9', 'J', 'Q', 'K', '2', '3', 'A'];
  var EMOTES = [
    { emoji: '🥶', chave: 'Emote_Gelado' }, { emoji: '😂', chave: 'Emote_Risada' }, { emoji: '😡', chave: 'Emote_Raiva' },
    { emoji: '😭', chave: 'Emote_Choro' }, { emoji: '😮', chave: 'Emote_Impressionado' }
  ];
  var TAXA_DO_BAU = 3;

  var textos = {};
  var visao = null;
  var anterior = null;
  var conexao = null;
  var diferencaDoRelogio = 0;
  var temporizador = null;
  var vooAtual = null;
  var jogadaPendente = false;
  var cartaPendente = null;
  var cartasAntes = 0;

  function $(seletor) { return document.querySelector(seletor); }

  function t(chave) {
    var modelo = Object.prototype.hasOwnProperty.call(textos, chave) ? textos[chave] : chave;
    var argumentos = Array.prototype.slice.call(arguments, 1);
    return modelo.replace(/\{(\d+)\}/g, function (_, i) { return argumentos[Number(i)] !== undefined ? argumentos[Number(i)] : ''; });
  }

  function el(tag, classe, texto) {
    var no = document.createElement(tag);
    if (classe) { no.className = classe; }
    if (texto !== undefined && texto !== null) { no.textContent = texto; }
    return no;
  }

  function evento(e) {
    return t.apply(null, [e.chave].concat(e.argumentos));
  }

  function carta(c, classes, aoClicar) {
    var no = el('button', 'c ' + (c.naipe <= 1 ? 'vermelha ' : '') + (classes || ''));
    no.type = 'button';
    no.setAttribute('aria-label', VALORES[c.valor] + NAIPES[c.naipe]);
    no.appendChild(el('span', 'r', VALORES[c.valor]));
    no.appendChild(el('span', 's', NAIPES[c.naipe]));
    no.appendChild(el('span', 'r r2', VALORES[c.valor]));
    if (aoClicar) { no.addEventListener('click', aoClicar); } else { no.tabIndex = -1; }
    return no;
  }

  function costas(classes) {
    var no = el('span', 'c verso ' + (classes || ''));
    return no;
  }

  function meuNome() {
    return visao.meuAssento >= 0 ? visao.assentos[visao.meuAssento].nome : '';
  }

  function nomeDe(assento) {
    return assento === visao.meuAssento ? t('Partida_Voce') : visao.assentos[assento].nome;
  }

  // ---------- conexão ----------

  function chamar(metodo) {
    var argumentos = Array.prototype.slice.call(arguments, 1);
    if (!conexao) { return; }
    conexao.invoke.apply(conexao, [metodo].concat(argumentos)).catch(function () {
      mostrarFaixa(t('Erro_Generico'), 'derrota');
    });
  }

  function mostrarFaixa(texto, tipo) {
    var faixa = $('#faixa');
    faixa.textContent = texto;
    faixa.className = 'faixa' + (tipo ? ' ' + tipo : '');
  }

  function conectar() {
    conexao = new signalR.HubConnectionBuilder()
      .withUrl('/hub/mesa')
      .withAutomaticReconnect()
      .build();

    conexao.on('MesaAtualizada', function (v) { aplicar(v); });
    conexao.on('EmoteRecebido', function (assento, emote) { mostrarEmote(assento, EMOTES[emote].emoji); });
    conexao.on('Erro', function (chave) {
      desfazerJogada();
      mostrarFaixa(t(chave), 'derrota');
    });
    conexao.on('SessaoSubstituida', function () {
      mostrarFaixa(t('Sala_SessaoSubstituida'), 'derrota');
      conexao.stop();
    });
    conexao.onreconnecting(function () { mostrarFaixa(t('Sala_ConexaoPerdida'), 'derrota'); });
    conexao.onreconnected(function () { mostrarFaixa('', ''); });

    conexao.start().catch(function () { mostrarFaixa(t('Sala_ConexaoPerdida'), 'derrota'); });
  }

  // ---------- desenho ----------

  function aplicar(nova) {
    anterior = visao;
    visao = nova;
    diferencaDoRelogio = Date.parse(nova.agoraUtc) - Date.now();
    $('#nome-sala').textContent = nova.nome;
    document.body.classList.add('mesa-aberta');

    app.classList.toggle('em-partida', nova.fase === 'partida');
    document.body.classList.toggle('partida-ativa', nova.fase === 'partida');
    if (nova.fase === 'partida') { document.body.classList.remove('menu-aberto'); }
    $('#espera').hidden = nova.fase !== 'espera';
    $('#partida').hidden = nova.fase !== 'partida';
    $('#fim').hidden = nova.fase !== 'encerrada';

    if (nova.fase === 'espera') { desenharEspera(); mostrarFaixa('', ''); }
    if (nova.fase === 'partida') { desenharPartida(); }
    // Só confirma a jogada otimista quando a carta realmente saiu da mão (ou a etapa mudou).
    if (!jogadaPendente || nova.fase !== 'partida' || nova.etapa !== 'rodadas' || nova.minhaMao.length < cartasAntes) {
      limparVoo();
    }
    if (nova.fase === 'encerrada') { desenharFim(); }
  }

  function desenharEspera() {
    var lista = $('#lista-espera');
    lista.textContent = '';
    visao.assentos.forEach(function (a) {
      var item = el('li');
      var avatar = el('div', 'av', a.nome.charAt(0).toUpperCase());
      avatar.style.background = a.cor;
      item.appendChild(avatar);
      item.appendChild(el('span', '', a.nome + (a.ehBot ? ' (' + t('Sala_Bot') + ')' : '') + (a.voce ? ' — ' + t('Partida_Voce') : '')));
      lista.appendChild(item);
    });
    $('#controles-criador').hidden = !visao.souCriador;
    $('#espera-aviso').hidden = visao.souCriador;
    $('#btn-iniciar').disabled = visao.assentos.length < 3;
    $('#btn-bots').disabled = visao.assentos.length >= visao.maximo;
  }

  function mensagemDaFaixa() {
    var v = visao;
    if (v.etapa === 'fim') {
      if (v.vencedorDaRodada !== null && v.vencedorDaRodada !== undefined) {
        var valor = v.valorDaRodada;
        var euVenci = v.vencedorDaRodada === v.meuAssento;
        return {
          texto: euVenci ? t('Partida_VoceVenceuRodada', valor) : t('Partida_VenceuRodada', nomeDe(v.vencedorDaRodada), valor),
          tipo: euVenci ? 'vitoria' : (v.perdedores.indexOf(v.meuAssento) >= 0 ? 'derrota' : 'vez')
        };
      }
      return { texto: v.aviso ? evento(v.aviso) : '', tipo: 'vez' };
    }
    if (v.vez !== null && v.vez !== undefined) {
      if (v.vez === v.meuAssento) {
        if (v.etapa === 'decisao') {
          var d = v.decisao;
          var texto = d.jaPegouBau ? t('Partida_VezBauPego')
            : !d.podeFicar ? t('Partida_VezSemFichas', v.montante)
            : v.baus.some(function (b) { return b.disponivel; }) ? t('Partida_VezDecidir') : t('Partida_VezDecidirSemBau');
          return { texto: texto, tipo: 'minha' };
        }
        return { texto: v.abre === v.meuAssento && v.jogadas.length === 0 ? t('Partida_VezAbrir') : t('Partida_VezJogar'), tipo: 'minha' };
      }
      return { texto: t('Partida_VezDe', nomeDe(v.vez)), tipo: 'vez' };
    }
    return { texto: '', tipo: '' };
  }

  function desenharPartida() {
    var v = visao;
    $('#info-mao').textContent = t('Partida_Mao', v.mao, v.assentos.filter(function (a) { return a.estaNaPartida; }).length) +
      (v.montante ? ' · ' + t('Partida_MontanteInfo', v.montante, v.valorDaRodada) : '');

    var m = mensagemDaFaixa();
    mostrarFaixa(m.texto, m.tipo);

    desenharMesa();
    animarJogadasNovas();
    desenharMao();
    desenharAcoes();
    desenharDiario();
    animarFichas();
  }

  // Você sempre fica embaixo (90°) e os demais seguem em sentido horário; o 12h continua marcado na etiqueta.
  function anguloDe(assento, total) {
    var base = visao.meuAssento >= 0 ? visao.meuAssento : 0;
    return (90 + (assento - base) * 360 / total) * Math.PI / 180;
  }

  function desenharMesa() {
    var v = visao;
    var mesa = $('#mesa');
    mesa.textContent = '';
    mesa.appendChild(el('div', 'tampo'));
    mesa.appendChild(el('div', 'pano'));

    var n = v.assentos.length;
    // A mesa é oval: aproveita a largura da tela; rx e ry são porcentagens da largura e da altura.
    var rx = 44;
    var ry = window.innerWidth <= 520 ? 41 : 43;
    v.assentos.forEach(function (a) {
      var angulo = anguloDe(a.assento, n);
      var venceu = v.vencedorDaRodada === a.assento;
      var perdeu = v.perdedores.indexOf(a.assento) >= 0;
      var classes = 'seat' + (!a.estaNaPartida ? ' fora' : '') + (v.vez === a.assento ? ' vez' : '') +
        (venceu ? ' venceu' : '') + (perdeu ? ' perdeu' : '') + (a.decisao === 'Correu' || a.decisao === 'Ausente' ? ' correu' : '');
      var lugar = el('div', classes);
      lugar.dataset.assento = a.assento;
      lugar.style.left = (50 + rx * Math.cos(angulo)) + '%';
      lugar.style.top = (50 + ry * Math.sin(angulo)) + '%';

      var avatar = el('div', 'av', a.nome.charAt(0).toUpperCase());
      avatar.style.background = a.cor;
      lugar.appendChild(avatar);
      var placa = el('div', 'placa');
      placa.appendChild(el('div', 'nm', a.voce ? t('Partida_Voce') : a.nome));
      var fichas = el('div', 'fichas');
      fichas.appendChild(el('span', 'moeda'));
      fichas.appendChild(document.createTextNode(String(a.fichas)));
      placa.appendChild(fichas);
      lugar.appendChild(placa);

      var etiquetas = el('div', 'tags');
      if (a.assento === 0) { etiquetas.appendChild(el('span', 'tag h', t('Partida_TagDoze'))); }
      if (a.ehDistribuidor && a.estaNaPartida) { etiquetas.appendChild(el('span', 'tag d', t('Partida_TagDistribuidor'))); }
      if (a.decisao === 'Ficou') { etiquetas.appendChild(el('span', 'tag ok', t('Partida_TagFicou'))); }
      if (a.decisao === 'Correu') { etiquetas.appendChild(el('span', 'tag no', t('Partida_TagCorreu'))); }
      if (a.decisao === 'Ausente') { etiquetas.appendChild(el('span', 'tag no', t('Partida_TagAusente'))); }
      if (a.baus > 0) { etiquetas.appendChild(el('span', 'tag bau', '🧰' + (a.baus > 1 ? '×2' : ''))); }
      if (v.abre === a.assento && v.etapa === 'rodadas') { etiquetas.appendChild(el('span', 'tag abre', t('Partida_TagAbre'))); }
      if (v.vez === a.assento) { etiquetas.appendChild(el('span', 'tag vez', t('Partida_TagVez'))); }
      if (v.proximo === a.assento) { etiquetas.appendChild(el('span', 'tag proximo', t('Partida_TagProximo'))); }
      if (venceu) { etiquetas.appendChild(el('span', 'tag w', t('Partida_TagVenceu'))); }
      if (perdeu) { etiquetas.appendChild(el('span', 'tag l', t('Partida_TagPerdeu'))); }
      lugar.appendChild(etiquetas);
      lugar.appendChild(el('div', 'pips', '●'.repeat(a.vitorias)));
      mesa.appendChild(lugar);
    });

    v.jogadas.forEach(function (j) {
      var angulo = anguloDe(j.assento, n);
      var lugar = el('div', 'jogada');
      lugar.dataset.assento = j.assento;
      lugar.style.left = (50 + 0.52 * rx * Math.cos(angulo)) + '%';
      lugar.style.top = (50 + 0.5 * ry * Math.sin(angulo) + 4) + '%';
      var classe = 'peq';
      if (j.carta) {
        if (v.vencedorDaRodada === j.assento) { classe += ' venceu'; }
        else if (v.perdedores.indexOf(j.assento) >= 0) { classe += ' perdeu'; }
        else if (v.mania && j.carta.naipe === v.mania.naipe) { classe += ' dourada'; }
        lugar.appendChild(carta(j.carta, classe));
      } else {
        lugar.appendChild(costas('peq'));
      }
      mesa.appendChild(lugar);
    });

    var centro = el('div', 'centro');
    if (v.mania) {
      centro.appendChild(el('div', '', t('Partida_Mania')));
      centro.appendChild(carta(v.mania, 'peq dourada'));
    }
    var pote = el('div', 'pote');
    pote.appendChild(el('span', 'moeda'));
    pote.appendChild(document.createTextNode(String(v.montante)));
    centro.appendChild(pote);
    mesa.appendChild(centro);

    // Os baús só existem durante a decisão: depois que todos decidem, somem da mesa.
    (v.etapa === 'decisao' ? v.baus : []).forEach(function (b) {
      var no = el('button', 'bau-mesa' + (b.dono !== null && b.dono !== undefined ? ' pego' : '') + (b.disponivel ? ' escolher' : ''));
      no.type = 'button';
      no.style.left = (b.numero ? 60 : 40) + '%';
      no.disabled = !b.disponivel;
      var rotulo = b.dono !== null && b.dono !== undefined ? nomeDe(b.dono) : t('Partida_Bau', b.numero + 1).replace('🧰 ', '');
      no.appendChild(el('span', 'rotulo', rotulo));
      if (b.disponivel) { no.addEventListener('click', function () { chamar('PegarBau', b.numero); }); }
      mesa.appendChild(no);
    });
  }

  function desenharMao() {
    var v = visao;
    var mao = $('#mao');
    mao.textContent = '';
    var eu = v.meuAssento >= 0 ? v.assentos[v.meuAssento] : null;
    if (eu && !eu.estaNaPartida) {
      var motivo = eu.saida === 'Inatividade' ? 'Partida_VoceEliminado' : eu.saida === 'SemFichas' ? 'Partida_VoceSemFichas' : 'Partida_VoceSaiu';
      mao.appendChild(el('p', 'nota', t(motivo)));
      return;
    }
    v.minhaMao.forEach(function (c, indice) {
      var jogavel = v.etapa === 'rodadas' && v.vez === v.meuAssento;
      var classes = '';
      var clique = null;
      if (jogavel) {
        var permitida = v.permitidas.indexOf(indice) >= 0;
        classes = permitida ? 'ok' : 'apagada';
        clique = function (evento) { jogarComAnimacao(evento.currentTarget, c); };
      } else if (eu && eu.decisao === 'Correu') {
        classes = 'apagada';
      }
      var botao = carta(c, classes, clique);
      if (cartaPendente && c.naipe === cartaPendente.naipe && c.valor === cartaPendente.valor) {
        botao.style.visibility = 'hidden';
      }
      mao.appendChild(botao);
    });
  }

  function desenharAcoes() {
    var v = visao;
    var acoes = $('#acoes');
    acoes.textContent = '';
    var minhaVez = v.vez === v.meuAssento && v.meuAssento >= 0 && v.prazoFimUtc;
    $('#bandeja').classList.toggle('minha', Boolean(minhaVez));
    window.clearInterval(temporizador);
    if (!minhaVez) { return; }

    var cronometro = el('div', 'timer');
    var barra = el('div', 'barra');
    var numero = el('b');
    cronometro.appendChild(barra);
    cronometro.appendChild(numero);
    acoes.appendChild(cronometro);
    var total = v.prazoSegundos * 1000;
    function tique() {
      var restante = Date.parse(v.prazoFimUtc) - (Date.now() + diferencaDoRelogio);
      barra.style.width = Math.max(0, Math.min(100, restante / total * 100)) + '%';
      numero.textContent = t('Partida_Segundos', Math.max(0, Math.ceil(restante / 1000)));
      cronometro.classList.toggle('baixo', restante < 5000);
    }
    tique();
    temporizador = window.setInterval(tique, 200);

    if (v.etapa !== 'decisao') { return; }
    var d = v.decisao;
    if (d.jaPegouBau) {
      acoes.appendChild(botao(t('Partida_JogarComEstaMao'), 'btn', function () { chamar('Decidir', true); }));
    } else {
      var ficar = botao(t('Partida_Ficar'), 'btn', function () { chamar('Decidir', true); });
      ficar.disabled = !d.podeFicar;
      acoes.appendChild(ficar);
      acoes.appendChild(botao(t('Partida_Correr'), 'btn red', function () { chamar('Decidir', false); }));
    }
    v.baus.forEach(function (b) {
      var rotulo = d.jaPegouBau ? t('Partida_BauTaxa', b.numero + 1, TAXA_DO_BAU) : t('Partida_Bau', b.numero + 1);
      var no = botao(rotulo, 'btn', function () { chamar('PegarBau', b.numero); });
      no.disabled = !b.disponivel;
      acoes.appendChild(no);
    });
  }

  function botao(texto, classe, aoClicar) {
    var no = el('button', classe, texto);
    no.type = 'button';
    no.addEventListener('click', aoClicar);
    return no;
  }

  function desenharDiario() {
    var log = $('#log');
    log.textContent = '';
    visao.diario.slice(-40).forEach(function (e) { log.appendChild(el('div', '', evento(e))); });
    log.scrollTop = log.scrollHeight;
  }

  // ---------- carta saindo da mão ----------

  function semMovimento() {
    return document.body.classList.contains('reduzir-animacoes') ||
      (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);
  }

  function ryDaMesa() { return window.innerWidth <= 520 ? 41 : 43; }

  /** Confirma a jogada. O clone só some depois que a animação terminar (ou à força, se o servidor recusar). */
  function limparVoo(forcar) {
    jogadaPendente = false;
    cartaPendente = null;
    if (!vooAtual) { return; }
    if (vooAtual.terminou || forcar) {
      vooAtual.remove();
      vooAtual = null;
    } else {
      vooAtual.confirmado = true;
    }
  }

  function desfazerJogada() {
    if (!jogadaPendente) { return; }
    limparVoo(true);
    if (visao && visao.fase === 'partida') { desenharMao(); }
  }

  /**
   * A carta sai da mão no mesmo instante do clique e voa até o lugar dela na mesa; o servidor confirma depois.
   * Se ele recusar, a carta volta para a mão (evento Erro).
   */
  function jogarComAnimacao(no, carta) {
    if (jogadaPendente) { return; }
    jogadaPendente = true;
    cartaPendente = carta;
    cartasAntes = visao.minhaMao.length;
    var origem = no.getBoundingClientRect();
    var mesa = $('#mesa').getBoundingClientRect();
    var tw = parseFloat(window.getComputedStyle(document.documentElement).getPropertyValue('--tw')) || 46;
    var alvoX = mesa.left + mesa.width / 2;
    var alvoY = mesa.top + mesa.height * (0.5 + 0.5 * ryDaMesa() / 100 + 0.04);

    if (!semMovimento()) {
      var voo = no.cloneNode(true);
      voo.classList.remove('ok', 'apagada');
      voo.style.position = 'fixed';
      voo.style.margin = '0';
      voo.style.left = origem.left + 'px';
      voo.style.top = origem.top + 'px';
      voo.style.zIndex = '70';
      $('#fx').appendChild(voo);
      var escala = (tw * 1.0) / origem.width;
      var dx = alvoX - (origem.left + origem.width / 2);
      var dy = alvoY - (origem.top + origem.height / 2);
      var animacao = voo.animate([
        { transform: 'translate(0,0) scale(1) rotate(0deg)' },
        { transform: 'translate(' + dx + 'px,' + dy + 'px) scale(' + escala + ') rotate(-5deg)' }
      ], { duration: 260, easing: 'cubic-bezier(.2,.8,.2,1)', fill: 'forwards' });
      animacao.onfinish = function () {
        voo.terminou = true;
        if (voo.confirmado) {
          voo.remove();
          if (vooAtual === voo) { vooAtual = null; }
        }
      };
      vooAtual = voo;
    }
    no.style.visibility = 'hidden';
    window.setTimeout(function () { if (jogadaPendente) { limparVoo(true); } }, 4000);
    chamar('JogarCarta', carta.naipe, carta.valor);
  }

  /** Cartas de outros jogadores que acabaram de ser jogadas voam do avatar até a mesa. */
  function animarJogadasNovas() {
    if (semMovimento() || !anterior || anterior.mao !== visao.mao || anterior.fase !== 'partida') { return; }
    var jaVistas = {};
    anterior.jogadas.forEach(function (j) { jaVistas[j.assento] = true; });
    visao.jogadas.forEach(function (j) {
      if (jaVistas[j.assento] || j.assento === visao.meuAssento) { return; }
      var destino = document.querySelector('#mesa .jogada[data-assento="' + j.assento + '"]');
      var avatar = lugarDe(j.assento);
      if (!destino || !avatar || !destino.firstElementChild) { return; }
      var a = avatar.getBoundingClientRect();
      var d = destino.getBoundingClientRect();
      var dx = (a.left + a.width / 2) - (d.left + d.width / 2);
      var dy = (a.top + a.height / 2) - (d.top + d.height / 2);
      destino.firstElementChild.animate([
        { transform: 'translate(' + dx + 'px,' + dy + 'px) scale(.55) rotate(8deg)', opacity: 0.2 },
        { transform: 'translate(0,0) scale(1) rotate(0deg)', opacity: 1 }
      ], { duration: 320, easing: 'cubic-bezier(.2,.8,.2,1)' });
    });
  }

  function desenharFim() {
    mostrarFaixa('', '');
    var lista = visao.assentos.slice().sort(function (a, b) { return b.fichas - a.fichas; });
    var quadro = $('#classificacao');
    quadro.textContent = '';
    var cabecalho = el('div');
    cabecalho.appendChild(el('b', '', t('Partida_ClassificacaoJogador')));
    cabecalho.appendChild(el('b', '', t('Partida_ClassificacaoFichas')));
    quadro.appendChild(cabecalho);
    lista.forEach(function (a) {
      var linha = el('div');
      linha.appendChild(el('span', '', (a.estaNaPartida ? '' : '🚪 ') + a.nome + (a.voce ? ' (' + t('Partida_Voce') + ')' : '')));
      linha.appendChild(el('span', '', String(a.fichas)));
      quadro.appendChild(linha);
    });
  }

  // ---------- efeitos ----------

  function centroDaMesa() { return document.querySelector('#mesa .centro'); }

  function lugarDe(assento) { return document.querySelector('#mesa .seat[data-assento="' + assento + '"] .av'); }

  function voarMoedas(de, para, quantidade) {
    if (!de || !para || document.body.classList.contains('reduzir-animacoes')) { return; }
    var a = de.getBoundingClientRect();
    var b = para.getBoundingClientRect();
    var tamanho = window.innerWidth <= 520 ? 22 : 28;
    var x0 = a.left + a.width / 2 - tamanho / 2;
    var y0 = a.top + a.height / 2 - tamanho / 2;
    var dx = b.left + b.width / 2 - tamanho / 2 - x0;
    var dy = b.top + b.height / 2 - tamanho / 2 - y0;
    for (var i = 0; i < quantidade; i++) {
      var moeda = el('span', 'fmoeda');
      moeda.style.width = moeda.style.height = tamanho + 'px';
      moeda.style.left = x0 + 'px';
      moeda.style.top = y0 + 'px';
      $('#fx').appendChild(moeda);
      var animacao = moeda.animate([
        { transform: 'translate(0,0) scale(.5)', opacity: 0 },
        { transform: 'translate(' + ((Math.random() - 0.5) * 36) + 'px,-22px) scale(1.15)', opacity: 1, offset: 0.22 },
        { transform: 'translate(' + dx + 'px,' + dy + 'px) scale(.95)', opacity: 1, offset: 0.88 },
        { transform: 'translate(' + dx + 'px,' + dy + 'px) scale(.4)', opacity: 0 }
      ], { duration: 950, delay: i * 120, easing: 'cubic-bezier(.5,0,.3,1)', fill: 'both' });
      animacao.onfinish = (function (no) { return function () { no.remove(); }; })(moeda);
    }
  }

  function animarFichas() {
    if (!anterior || anterior.fase !== 'partida' || anterior.mao !== visao.mao && anterior.mao > visao.mao) { return; }
    visao.assentos.forEach(function (a) {
      var antes = anterior.assentos[a.assento];
      if (!antes) { return; }
      var diferenca = a.fichas - antes.fichas;
      if (diferenca > 0) { voarMoedas(centroDaMesa(), lugarDe(a.assento), Math.min(diferenca, 8)); }
      if (diferenca < 0) { voarMoedas(lugarDe(a.assento), centroDaMesa(), Math.min(-diferenca, 8)); }
    });
  }

  function mostrarEmote(assento, emoji) {
    var avatar = lugarDe(assento);
    if (!avatar) { return; }
    var r = avatar.getBoundingClientRect();
    var balao = el('span', 'balao', emoji);
    balao.style.left = (r.left + r.width / 2) + 'px';
    balao.style.top = (r.top - 58) + 'px';
    $('#fx').appendChild(balao);
    var movimento = document.body.classList.contains('reduzir-animacoes');
    var quadros = movimento
      ? [{ transform: 'translate(-50%,0)', opacity: 1 }, { transform: 'translate(-50%,0)', opacity: 1, offset: 0.8 }, { transform: 'translate(-50%,0)', opacity: 0 }]
      : [
        { transform: 'translate(-50%,10px) scale(.3)', opacity: 0 },
        { transform: 'translate(-50%,0) scale(1.2)', opacity: 1, offset: 0.14 },
        { transform: 'translate(-50%,0) scale(1)', opacity: 1, offset: 0.22 },
        { transform: 'translate(-50%,-6px) scale(1)', opacity: 1, offset: 0.8 },
        { transform: 'translate(-50%,-26px) scale(.9)', opacity: 0 }
      ];
    var animacao = balao.animate(quadros, { duration: 2600, easing: 'ease-out', fill: 'both' });
    animacao.onfinish = function () { balao.remove(); };
  }

  // ---------- controles fixos ----------

  function ligarControles() {
    $('#btn-bots').addEventListener('click', function () { chamar('PreencherComBots', Number($('#qtd-bots').value)); });
    $('#btn-iniciar').addEventListener('click', function () { chamar('IniciarPartida'); });
    $('#btn-lobby').addEventListener('click', function () {
      chamar('SairDaSala');
      window.location.href = '/salas';
    });
    $('#btn-sair').addEventListener('click', function () {
      var emJogo = visao && visao.fase === 'partida' && visao.meuAssento >= 0 && visao.assentos[visao.meuAssento].estaNaPartida;
      if (emJogo && !window.confirm(t('Partida_ConfirmarSaida'))) { return; }
      conexao.invoke('SairDaSala').finally(function () { window.location.href = '/salas'; });
    });
    $('#btn-regras').addEventListener('click', function () { $('#janela-regras').showModal(); });
    $('#btn-fechar-regras').addEventListener('click', function () { $('#janela-regras').close(); });

    var painel = $('#painel-emotes');
    EMOTES.forEach(function (e, indice) {
      var b = el('button', 'emo', e.emoji);
      b.type = 'button';
      b.title = t(e.chave);
      b.setAttribute('aria-label', t(e.chave));
      b.addEventListener('click', function () {
        painel.classList.remove('aberto');
        chamar('EnviarEmote', indice);
      });
      painel.appendChild(b);
    });
    $('#btn-emotes').addEventListener('click', function () { painel.classList.toggle('aberto'); });
  }

  fetch('/recursos/' + app.dataset.idioma + '.json', { credentials: 'same-origin' })
    .then(function (resposta) { return resposta.json(); })
    .then(function (dados) {
      textos = dados;
      ligarControles();
      conectar();
    });
})();
