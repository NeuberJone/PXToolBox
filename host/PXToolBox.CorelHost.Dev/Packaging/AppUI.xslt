<?xml version="1.0"?>
<!--
PX Tool Box DEV — registro do docker nativo no CorelDRAW.

Correção (branch fix/dev-docker-hosting): a versão anterior deste arquivo inseria o item
"wpfhost" diretamente dentro de `uiConfig/commandBars` (container solto), copiando um padrão
visto no add-on SISBolt já instalado nesta máquina. Isso NÃO cria uma janela de encaixe — apenas
embute o controle dentro de uma barra de comandos, por isso o painel aparecia comprimido na
barra superior ao lado do SISBolt.

O mecanismo correto (confirmado por inspeção de dois add-ons reais já instalados nesta máquina
que de fato abrem como docker nativo: `Programs64\Addons\MoldMotor\` e o exemplo oficial do SDK
da Corel `Programs64\Addons\eCutDocker\`) usa `uiConfig/dockers` com um elemento `dockerData`
próprio, mais um `checkButton` que alterna esse docker via `check="*Docker('<guid>')"`. Ver
docs/ARCHITECTURE.md, seção 3.

Os GUIDs abaixo são exclusivos do DEV — a distribuição Prod (futura) usará GUIDs diferentes,
nunca estes (ver AGENTS.md, "separação entre Produção e DEV"). `dynamicCategory` é um GUID fixo
do CorelDRAW (confirmado idêntico em MoldMotor e eCutDocker — não é específico de nenhum
produto), usado para agrupar botões de alternância de docker.
-->
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:frmwrk="Corel Framework Data">
  <xsl:output method="xml" encoding="UTF-8" indent="yes"/>

  <frmwrk:uiconfig>
    <frmwrk:applicationInfo userConfiguration="true" />
  </frmwrk:uiconfig>

  <xsl:template match="node()|@*">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/items">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>

      <!-- Botão que mostra/esconde o docker do PX Tool Box DEV. -->
      <itemData guid="0dd0d869-201a-40a9-b3d0-801f9138f700"
                noBmpOnMenu="true"
                type="checkButton"
                check="*Docker('332244c0-4471-4da8-bff8-1b2ed48a9dd8')"
                dynamicCategory="2cc24a3e-fe24-4708-9a74-9c75406eebcd"
                userCaption="PX Tool Box DEV"
                enable="true"/>

      <!-- Host WPF: o CorelDRAW instancia esta classe por reflexão e injeta a Application. -->
      <itemData guid="c7124037-25c8-4d03-80b2-97b716ed95ef"
                type="wpfhost"
                hostedType="Addons\PXToolBoxDev\PXToolBoxDev.dll,PXToolBoxDev.ControlUI"
                enable="true"/>

    </xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/dockers">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>

      <!-- O docker nativo em si — encaixável, redimensionável, pode ser fechado/reaberto. -->
      <dockerData guid="332244c0-4471-4da8-bff8-1b2ed48a9dd8"
                  userCaption="PX Tool Box DEV"
                  noPadding="true"
                  wantReturn="true"
                  focusStyle="noThrow">
        <container>
          <item dock="fill" margin="0,0,0,0" guidRef="c7124037-25c8-4d03-80b2-97b716ed95ef"/>
        </container>
      </dockerData>

    </xsl:copy>
  </xsl:template>

</xsl:stylesheet>
