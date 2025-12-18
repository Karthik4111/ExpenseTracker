pipeline {
    agent any

    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Restore') {
            steps {
                sh 'dotnet restore'
            }
        }

        stage('Build') {
            steps {
                sh 'dotnet build --configuration Release --no-restore'
            }
        }
    }

    post {
        success {
            echo 'ASP.NET Web API build successful'
        }
        failure {
            echo 'Build failed'
        }
    }
}
