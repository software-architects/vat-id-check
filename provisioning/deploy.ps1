$location = "westeurope"
$stage = "dev"
$resourceGroupName = "BillomatVatIdChecker$stage"


$deployFilePath = "deploy.json"
$parametersFilePath = "parameters-$stage.json"
 .\Deploy-AzTemplate.ps1 `
    -ArtifactStagingDirectory '.\' `
    -ResourceGroupName $resourceGroupName `
    -ResourceGroupLocation $location `
    -TemplateFile $deployFilePath `
    -TemplateParametersFile $parametersFilePath