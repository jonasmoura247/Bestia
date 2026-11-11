(function () {
  'use strict';

  var lista = document.getElementById('salas-abertas');
  if (!lista) { return; }

  var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');

  function formularioDeEntrar(codigo, rotulo) {
    var form = document.createElement('form');
    form.method = 'post';
    form.action = '/salas/entrar';
    var campos = { Codigo: codigo };
    if (tokenInput) { campos.__RequestVerificationToken = tokenInput.value; }
    Object.keys(campos).forEach(function (nome) {
      var campo = document.createElement('input');
      campo.type = 'hidden';
      campo.name = nome;
      campo.value = campos[nome];
      form.appendChild(campo);
    });
    var botao = document.createElement('button');
    botao.type = 'submit';
    botao.className = 'btn';
    botao.textContent = rotulo;
    form.appendChild(botao);
    return form;
  }

  function desenhar(salas) {
    lista.textContent = '';
    if (salas.length === 0) {
      var vazio = document.createElement('p');
      vazio.className = 'nota';
      vazio.textContent = lista.dataset.vazio;
      lista.appendChild(vazio);
      return;
    }
    salas.forEach(function (sala) {
      var item = document.createElement('div');
      item.className = 'sala-item';
      var info = document.createElement('div');
      var nome = document.createElement('b');
      nome.textContent = sala.nome;
      var detalhe = document.createElement('span');
      detalhe.className = 'nota';
      detalhe.textContent = lista.dataset.jogadores.replace('{0}', sala.jogadores).replace('{1}', sala.maximo);
      info.appendChild(nome);
      info.appendChild(detalhe);
      item.appendChild(info);
      if (sala.emPartida) {
        var aviso = document.createElement('span');
        aviso.className = 'nota';
        aviso.textContent = lista.dataset.emPartida;
        item.appendChild(aviso);
      } else {
        item.appendChild(formularioDeEntrar(sala.codigo, lista.dataset.sentar));
      }
      lista.appendChild(item);
    });
  }

  function atualizar() {
    fetch(lista.dataset.url, { credentials: 'same-origin' })
      .then(function (resposta) { return resposta.ok ? resposta.json() : null; })
      .then(function (salas) { if (salas) { desenhar(salas); } })
      .catch(function () { /* tenta de novo no próximo ciclo */ });
  }

  window.setInterval(atualizar, 5000);
})();
